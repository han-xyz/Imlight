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
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.Game.Minigames;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

/// <summary>
/// Runs the admin panel's client script for staff accounts and carries out the panel's requests.
/// </summary>
/// <remarks>
/// The script adds an Admin button to the settings window. Each action needs the account's own auth level, and
/// every change is audited. Requests and replies are tab-separated fields in a raw text message:
/// the client accepts process messages only from protocols it has loaded, which excludes the script debugger's.
/// Each page of the panel has its own handler, chosen by the request's prefix (for example "accounts.lookup").
/// </remarks>
internal class AdminPanelService(SessionActor sessionActor) : MessageService(sessionActor) {

    // Minigame jobs use random IDs below 0x80000000, so these cannot collide with them.
    private const uint FirstJobId = 0xAD000000;
    private const string ClientScript = "AdminPanel/Client.lua";

    private uint? _jobId;
    private uint _nextJobId = FirstJobId;

    internal static bool IsAdminPanelJob(uint jobId) => jobId >= FirstJobId;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new AdminPanelService(parentActor));

    private static bool Enabled {
        get {
            var raw = ConfigurationManager.GetSetting("Admin Panel.Enabled");

            return bool.TryParse(raw, out var enabled) && enabled;
        }
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete() {
        var wizard = GetActiveWizard();
        if (!Enabled || wizard.Account.AuthLevel == AuthLevel.None || MinigameConfig.IsMinigameZone(wizard.Zone)) {
            return;
        }

        // Whether client scripts survive a zone change is unknown, so restart the script to keep exactly one copy.
        if (_jobId is uint running) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_KILL_CLIENT_PROCESS { JobID = running });
        }

        var jobId = _nextJobId++;
        _jobId = jobId;
        SendToSocket(new GAME_5_PROTOCOL.MSG_START_CLIENT_PROCESS {
            OwnerGID = wizard.GameObjectID,
            JobID = jobId,
            ScriptName = ClientScript
        });
        Logger.Information("Admin panel: started the client script (job {0:X8}) for {1} in {2}.",
            Logger.Args(jobId, wizard.Account.Username, wizard.Zone));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_MESSAGE_PROCESS))]
    private void ReceiveMessageProcess(GAME_5_PROTOCOL.MSG_MESSAGE_PROCESS message) {
        if (_jobId != message.JobID) {
            return;
        }

        var wizard = GetActiveWizard();
        var account = wizard.Account;
        if (ProcessMessageCodec.Decode(message.Message) is not EXTENDEDBASE_2_PROTOCOL.MSG_RAW_TEXT request) {
            Logger.Warning("Admin panel: {0} sent an unrecognized message ({1} bytes).",
                Logger.Args(account.Username, ((byte[]) message.Message).Length));

            return;
        }

        // Fields: the action, then its arguments. Arguments are never logged: some are passwords.
        var fields = ((string) request.Message).Split('\t');
        var action = fields[0];
        Logger.Information("Admin panel: {0} requested '{1}'.", Logger.Args(account.Username, action));

        switch (action) {
            // The script reports these steps; they need no answer.
            case "started" or "open" or "close":
                break;
            case not null when action.StartsWith(AccountsPage.Prefix):
                AccountsPage.Handle(account, wizard, action[AccountsPage.Prefix.Length..], fields[1..], Reply);
                break;
            case not null when action.StartsWith(PlayersPage.Prefix):
                PlayersPage.Handle(account, wizard, action[PlayersPage.Prefix.Length..], fields[1..], Reply,
                    (accountId, change) => AccountAdministration.AdjustOnlineCrowns(Context, accountId, change));
                break;
            default:
                Reply(["status", "error", $"Unknown request '{action}'."]);
                break;
        }
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_PROCESS_TERMINATED))]
    private void ReceiveClientProcessTerminated(GAME_5_PROTOCOL.MSG_CLIENT_PROCESS_TERMINATED message) {
        if (_jobId != message.JobID) {
            return;
        }

        _jobId = null;
        Logger.Information("Admin panel: the client ended the script (job {0:X8}).", Logger.Args(message.JobID));
    }

    // Tabs separate the fields, so any inside a value become spaces.
    private void Reply(string[] fields)
        => SendToSocket(new GAME_5_PROTOCOL.MSG_MESSAGE_PROCESS {
            JobID = _jobId ?? 0,
            Message = ProcessMessageCodec.Encode(new EXTENDEDBASE_2_PROTOCOL.MSG_RAW_TEXT {
                Message = string.Join('\t', Array.ConvertAll(fields, field => (field ?? "").Replace('\t', ' ')))
            })
        });

}
