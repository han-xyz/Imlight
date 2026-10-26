/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * TEAM UP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Answers the team-up window and the Team Help kiosk: queues the wizard for a dungeon sigil, shows each queue's
 * players, and sends a formed team into the dungeon as one group through the dungeon run book.
 * 
 * USAGE EXAMPLE:
 * MSG_REQUESTTEAMUP names the sigil object; its zone answers with the sigil's destination.
 * 
 * NOTE:
 * The team-help info strings ("sigilId:name:queued;...") and the school string in MSG_TEAMUPADDPLAYERTOCLIENT
 * are read from the message names only; no capture of the retail format exists.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Services;

internal class TeamUpService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string CheckTimerKey = "teamup-check";
    private static readonly TimeSpan s_sigilQueryTimeout = TimeSpan.FromSeconds(2);

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TeamUpService(parentActor));

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMUP))]
    private void ReceiveRequestTeamUp(WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMUP message) {
        if (!TryConfirmSigil(message.NPCID, out var dungeon, out var sigil)) {
            Logger.Debug("Team-up request for object {NpcId}: no dungeon sigil answered.", Logger.Args(message.NPCID));
            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPFAIL { TimeLeft = 0 });

            return;
        }

        JoinQueue(dungeon, sigil);
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPJOIN))]
    private void ReceiveRequestTeamHelpJoin(WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPJOIN message) {
        var queued = TeamUpQueue.Shared.FindSigil(message.SigilID);
        if (queued is null || !TryConfirmSigil(queued.Gid, out var dungeon, out var sigil)) {
            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPJOINFAIL { PlayerGID = GetActiveWizard().CharId });

            return;
        }

        JoinQueue(dungeon, sigil);
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_TEAMUPREMOVEPLAYER))]
    private void ReceiveTeamUpRemovePlayer(WIZARD2_53_PROTOCOL.MSG_TEAMUPREMOVEPLAYER message)
        => LeaveQueue();

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPINFO))]
    private void ReceiveRequestTeamHelpInfo(WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPINFO message)
        => SendToSocket(new WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPINFO {
            WorldInfo = string.Empty,
            SigilInfo = DescribeOwnQueue()
        });

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_CLIENTREQUESTTEAMHELPWORLDINFO))]
    private void ReceiveClientRequestTeamHelpWorldInfo(WIZARD2_53_PROTOCOL.MSG_CLIENTREQUESTTEAMHELPWORLDINFO message)
        => SendToSocket(new WIZARD2_53_PROTOCOL.MSG_REQUESTTEAMHELPWORLDINFO {
            WorldID = message.WorldID,
            WorldSigilInfo = DescribeOwnQueue(),
            PlayerGID = GetActiveWizard().CharId,
            ClientPlatform = message.ClientPlatform
        });

    [MessageHandler(typeof(TeamUpCheck))]
    private void ReceiveTeamUpCheck(TeamUpCheck message)
        => FormReadyTeams();

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message)
        => LeaveQueue();

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message)
        => LeaveQueue();

    private bool TryConfirmSigil(ulong sigilGid, out TeamUpDungeon dungeon, out TeamUpSigil sigil) {
        dungeon = null;
        sigil = null;
        var wizard = GetActiveWizard();
        var zoneActor = SessionActor.GetZoneActor();
        if (wizard is null || zoneActor is null) {
            return false;
        }

        try {
            var answer = zoneActor.Ask<ZONE_102_PROTOCOL.MSG_TEAMUPDUNGEON>(
                new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
                    Sender = SessionActor.ActorRef,
                    Messages = [new ZONE_102_PROTOCOL.MSG_TEAMUPSIGILQUERY {
                        SigilGID = sigilGid,
                        RequesterCharId = wizard.CharId,
                        RequesterZone = wizard.Zone,
                        RequesterLocation = wizard.Location
                    }],
                    Targets = ZoneBroadcastTarget.Objects | ZoneBroadcastTarget.Sigils
                },
                s_sigilQueryTimeout).Result;

            dungeon = new TeamUpDungeon(answer.DestinationZone, answer.DestinationLoc, answer.SigilName);
            sigil = new TeamUpSigil(answer.SigilZone, answer.SigilGID, answer.PadLocation, answer.Radius);

            return true;
        } catch (AggregateException) {
            return false;
        }
    }

    private void JoinQueue(TeamUpDungeon dungeon, TeamUpSigil sigil) {
        var wizard = GetActiveWizard();
        var sigilId = TeamUpQueue.Shared.Enqueue(wizard.CharId, dungeon, sigil);
        if (sigilId == 0) {
            return;
        }

        var secondsLeft = TeamUpQueue.Shared.SecondsLeft(sigilId);

        foreach (var memberId in TeamUpQueue.Shared.MembersOf(sigilId)) {
            SendToPlayer(wizard.CharId, BuildAddPlayer(sigilId, dungeon, memberId, wizard.CharId, secondsLeft));
            if (memberId != wizard.CharId) {
                SendToPlayer(memberId, BuildAddPlayer(sigilId, dungeon, wizard.CharId, memberId, secondsLeft));
            }
        }

        Logger.Debug("Character {CharId} queued for team-up to '{Zone}' (sigil {SigilId}).",
            Logger.Args(wizard.CharId, dungeon.DungeonZone, sigilId));

        FormReadyTeams();
        Timers.StartSingleTimer(CheckTimerKey, new TeamUpCheck(), TimeSpan.FromSeconds(secondsLeft + 1));
    }

    private void LeaveQueue() {
        var charId = GetActiveWizard()?.CharId ?? 0;
        var sigilId = TeamUpQueue.Shared.SigilIdOf(charId);
        if (sigilId == 0) {
            return;
        }

        var remaining = TeamUpQueue.Shared.MembersOf(sigilId).Where(memberId => memberId != charId).ToList();
        TeamUpQueue.Shared.Remove(charId);
        Timers.Cancel(CheckTimerKey);

        foreach (var memberId in remaining.Append(charId)) {
            SendToPlayer(memberId, new WIZARD2_53_PROTOCOL.MSG_TEAMUPREMOVEPLAYERFROMCLIENT {
                SigilID = sigilId,
                PlayerGID = charId,
                UpdatePlayerGID = memberId,
                StopTimer = (byte) (memberId == charId ? 1 : 0)
            });
        }
    }

    private void FormReadyTeams() {
        var teams = TeamUpQueue.Shared.TakeReadyTeams(IsStillAtSigil, out var dropped);
        foreach (var (charId, sigilId) in dropped) {
            Logger.Debug("Character {CharId} dropped from team-up {SigilId}: no longer at the sigil.",
                Logger.Args(charId, sigilId));

            SendToPlayer(charId, new WIZARD2_53_PROTOCOL.MSG_TEAMUPREMOVEPLAYERFROMCLIENT {
                SigilID = sigilId,
                PlayerGID = charId,
                UpdatePlayerGID = charId,
                StopTimer = 1
            });
        }

        foreach (var team in teams) {
            Logger.Information("Team-up team of {Count} formed for '{Zone}'.",
                Logger.Args(team.Members.Count, team.Dungeon.DungeonZone));

            GroupDirectory.Instance?.Tell(new GROUP_109_PROTOCOL.MSG_FORMTEAM {
                MemberCharIds = team.Members,
                MemberMessage = new ZONE_102_PROTOCOL.MSG_TEAMUPENTER {
                    DestinationZone = team.Dungeon.DungeonZone,
                    DestinationLoc = team.Dungeon.DestinationLoc
                }
            });
        }
    }

    private static bool IsStillAtSigil(ulong charId, TeamUpSigil sigil) {
        var member = WizardCollection.GetCharacter(charId);

        return member is not null
            && string.Equals(member.Zone, sigil.Zone, StringComparison.OrdinalIgnoreCase)
            && ZoneService.IsWithinSigilRange(sigil.PadLocation, sigil.Radius, member.Location);
    }

    private string DescribeOwnQueue() {
        var sigilId = TeamUpQueue.Shared.SigilIdOf(GetActiveWizard().CharId);
        var dungeon = TeamUpQueue.Shared.FindDungeon(sigilId);

        return dungeon is null
            ? string.Empty
            : $"{sigilId}:{dungeon.SigilName}:{TeamUpQueue.Shared.MembersOf(sigilId).Count}";
    }

    private static WIZARD2_53_PROTOCOL.MSG_TEAMUPADDPLAYERTOCLIENT BuildAddPlayer(uint sigilId, TeamUpDungeon dungeon,
                                                                                  ulong playerId, ulong recipientId,
                                                                                  uint secondsLeft) {
        var player = WizardCollection.GetCharacter(playerId);

        return new WIZARD2_53_PROTOCOL.MSG_TEAMUPADDPLAYERTOCLIENT {
            SigilID = sigilId,
            PlayerGID = playerId,
            PackedName = player?.PlayerNameBehavior.GetPackedNameBytes() ?? [],
            PlayerSchool = player?.MagicSchoolBehavior.MagicSchool.ToString() ?? string.Empty,
            PlayerLevel = (uint) (player?.MagicSchoolBehavior.Level ?? 0),
            SigilName = dungeon.SigilName,
            UpdatePlayerGID = recipientId,
            SecondsLeft = secondsLeft,
            FromSigil = 1
        };
    }

    private void SendToPlayer(ulong charId, IMessage message) {
        if (charId == GetActiveWizard()?.CharId) {
            SendToSocket(message);

            return;
        }

        if (TryGetOnlinePlayer(charId, out var player)) {
            Context.ActorSelection(player.ActorPath).Tell(message);
        }
    }

    private sealed record TeamUpCheck : IServerMessage {

        public byte MessageOrder => 0;
        public byte ServiceID => 102;

    }

}
