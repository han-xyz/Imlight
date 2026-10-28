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
 * CHAT SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages in-game chat functionality, including message processing, 
 * command handling, and logging of player communications.
 * 
 * USAGE EXAMPLE:
 * Internal service handling chat-related messages within the game server's 
 * session management system.
 * 
 * NOTE:
 * - Implements chat command processing for authorized users
 * 
 * TODO:
 * - Review and enhance chat command authorization
 * - Improve message sanitization logic
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class ChatService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string COMMAND_PREFIX = ".";

    private Wizard _selectedCharacter;
    private Account _selectedAccount;

    private readonly IActorRef _dispatcherRef = CommandDispatcher.Instance;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new ChatService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT))]
    private void ReceiveRequestRadialChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT message)
        => BroadcastRadialChat(message.Message);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALOWNERCHAT))]
    private void ReceiveRequestRadialOwnerChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALOWNERCHAT message)
        => BroadcastRadialChat(message.Message);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHATEXT))]
    private void ReceiveRequestRadialQuickChatExt(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHATEXT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        if (!TryCleanChatLine(message.Message, wizard, out var cleaned)) {
            return;
        }

        SendFilteredZoneMessage(new GAME_5_PROTOCOL.MSG_RADIALQUICKCHATEXT {
            Message = cleaned,
            SourceID = GetActiveGameObject().m_globalID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Filter = 0,
        }, wizard.CharId, wizard.Zone);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTZONECLUSTERQUICKCHAT))]
    private void ReceiveRequestZoneClusterQuickChat(GAME_5_PROTOCOL.MSG_REQUESTZONECLUSTERQUICKCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        SendFilteredZoneMessage(new GAME_5_PROTOCOL.MSG_RADIALZONECLUSTERQUICKCHAT {
            MessageID = message.MessageID,
            IsOwner = message.IsOwner,
            SourceID = GetActiveGameObject().m_globalID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Filter = 0,
        }, wizard.CharId, wizard.Zone);
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTZONECLUSTERQUICKCHATEXT))]
    private void ReceiveRequestZoneClusterQuickChatExt(WIZARD2_53_PROTOCOL.MSG_REQUESTZONECLUSTERQUICKCHATEXT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        if (!TryCleanChatLine(message.Message, wizard, out var cleaned)) {
            return;
        }

        SendFilteredZoneMessage(new WIZARD2_53_PROTOCOL.MSG_RADIALZONECLUSTERQUICKCHATEXT {
            Message = cleaned,
            IsOwner = message.IsOwner,
            SourceID = GetActiveGameObject().m_globalID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Filter = 0,
        }, wizard.CharId, wizard.Zone);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALFRIENDCHAT))]
    private void ReceiveRequestRadialFriendChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALFRIENDCHAT message)
        => SendRadialChatTo([message.PlayerGID], message.Message);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALFRIENDQUICKCHAT))]
    private void ReceiveRequestRadialFriendQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALFRIENDQUICKCHAT message)
        => SendQuickChatTo([message.PlayerGID], message.MessageID);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTRADIALFRIENDQUICKCHATEXT))]
    private void ReceiveRequestRadialFriendQuickChatExt(WIZARD2_53_PROTOCOL.MSG_REQUESTRADIALFRIENDQUICKCHATEXT message)
        => SendQuickChatExtTo([message.PlayerGID], message.Message);

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALTEAMUPCHAT))]
    private void ReceiveRequestRadialTeamUpChat(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALTEAMUPCHAT message)
        => SendRadialChatTo(TeamUpQueue.Shared.QueueMatesOf(GetActiveWizard().CharId), message.Message);

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALTEAMUPQUICKCHAT))]
    private void ReceiveRequestRadialTeamUpQuickChat(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALTEAMUPQUICKCHAT message)
        => SendQuickChatTo(TeamUpQueue.Shared.QueueMatesOf(GetActiveWizard().CharId), message.MessageID);

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALTEAMUPQUICKCHATEXT))]
    private void ReceiveRequestRadialTeamUpQuickChatExt(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALTEAMUPQUICKCHATEXT message)
        => SendQuickChatExtTo(TeamUpQueue.Shared.QueueMatesOf(GetActiveWizard().CharId), message.Message);

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALADVENTURECHAT))]
    private void ReceiveRequestRadialAdventureChat(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALADVENTURECHAT message)
        => Logger.Debug("Adventure party chat is not supported; dropped a line for party {PartyId}.", Logger.Args(message.PartyGID));

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALADVENTUREQUICKCHAT))]
    private void ReceiveRequestRadialAdventureQuickChat(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALADVENTUREQUICKCHAT message)
        => Logger.Debug("Adventure party chat is not supported; dropped a line for party {PartyId}.", Logger.Args(message.PartyGID));

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALADVENTUREQUICKCHATEXT))]
    private void ReceiveRequestRadialAdventureQuickChatExt(WIZARD3_56_PROTOCOL.MSG_REQUESTRADIALADVENTUREQUICKCHATEXT message)
        => Logger.Debug("Adventure party chat is not supported; dropped a line for party {PartyId}.", Logger.Args(message.PartyGID));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHATBYNAME))]
    private void ReceiveRequestDirectedChatByName(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHATBYNAME message) {
        // todo: names are stored packed, so a typed name has no lookup yet.
        Logger.Debug("Directed chat by name is not supported; answered as an unknown target.");
        SendToSocket(new GAME_5_PROTOCOL.MSG_DIRECTEDCHATFAIL());
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTCHATCODE))]
    private void ReceiveRequestChatCode(GAME_5_PROTOCOL.MSG_REQUESTCHATCODE message)
        => Logger.Debug("Chat codes are not supported; ignored a code request.");

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_USECHATCODE))]
    private void ReceiveUseChatCode(GAME_5_PROTOCOL.MSG_USECHATCODE message)
        => Logger.Debug("Chat codes are not supported; ignored a code for buddy {BuddyId}.", Logger.Args(message.BuddyID));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTCREATECHANNEL))]
    private void ReceiveRequestCreateChannel(GAME_5_PROTOCOL.MSG_REQUESTCREATECHANNEL message)
        => Logger.Debug("Chat channels are not supported; ignored a create request.");

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTJOINCHANNEL))]
    private void ReceiveRequestJoinChannel(GAME_5_PROTOCOL.MSG_REQUESTJOINCHANNEL message)
        => Logger.Debug("Chat channels are not supported; ignored a join request for {ChannelId}.", Logger.Args(message.ChannelID));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTJOINCHANNELBYNAME))]
    private void ReceiveRequestJoinChannelByName(GAME_5_PROTOCOL.MSG_REQUESTJOINCHANNELBYNAME message)
        => Logger.Debug("Chat channels are not supported; ignored a join request by name.");

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTLEAVECHANNEL))]
    private void ReceiveRequestLeaveChannel(GAME_5_PROTOCOL.MSG_REQUESTLEAVECHANNEL message)
        => Logger.Debug("Chat channels are not supported; ignored a leave request for {ChannelId}.", Logger.Args(message.ChannelID));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTLEAVECHANNELBYNAME))]
    private void ReceiveRequestLeaveChannelByName(GAME_5_PROTOCOL.MSG_REQUESTLEAVECHANNELBYNAME message)
        => Logger.Debug("Chat channels are not supported; ignored a leave request by name.");

    private void BroadcastRadialChat(ByteString text) {
        if (IsSenderMuted()) {
            return;
        }

        var charObj = GetActiveGameObject();
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();

        var cleanedMessage = RadialChatText.Clean(text);
        if (cleanedMessage.Length == 0) {
            return;
        }

        // Commands are never broadcast to the zone.
        if (cleanedMessage.StartsWith(COMMAND_PREFIX) && account.AuthLevel > AuthLevel.None) {
            SendChatCommand(cleanedMessage, charObj, wizard);

            return;
        }

        LogChatMessage(wizard.PlayerNameBehavior.GetWizardName(), cleanedMessage, wizard.Zone);
        SaveChatLog(cleanedMessage, wizard);

        var msg = new GAME_5_PROTOCOL.MSG_RADIALCHAT {
            Message = RadialChatText.Encode(cleanedMessage),
            SourceID = charObj.m_globalID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Filter = 2,
        };
        SendFilteredZoneMessage(msg, wizard.CharId, wizard.Zone);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
    private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        var msg = new GAME_5_PROTOCOL.MSG_RADIALQUICKCHAT() {
            MessageID = message.MessageID,
            SourceID = GetActiveGameObject().m_globalID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Filter = 0,
        };
        SendFilteredZoneMessage(msg, wizard.CharId, wizard.Zone);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHAT))]
    private void ReceiveWhisperRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        SendDirectedChat(message.TargetID, new GAME_5_PROTOCOL.MSG_DIRECTEDQUICKCHAT {
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            SourceID = wizard.CharId,
            MessageID = message.MessageID,
            Filter = 0
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHATEXT))]
    private void ReceiveWhisperRadialChatExt(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHATEXT message)
        => SendQuickChatExtTo([message.TargetID], message.Message);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT))]
    private void ReceiveWhisperDirectChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        // The client's /party line is a directed chat aimed at the group's channel ID.
        var wizard = GetActiveWizard();
        if (GroupRegistry.IsGroupChannel(message.TargetID)) {
            SendGroupChat(message, wizard);

            return;
        }

        SendDirectedChat(message.TargetID, new GAME_5_PROTOCOL.MSG_DIRECTEDCHAT {
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            SourceID = wizard.CharId,
            Message = message.Message,
            Filter = 0
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYSTATS))]
    private void ReceivePlayerSelect(GAME_5_PROTOCOL.MSG_BUDDYSTATS message) {
        var localAccount = GetActiveAccount();
        if (localAccount.AuthLevel < AuthLevel.HallMonitor) {
            return;
        }

        if (message.BuddyID == 0) {
            _selectedCharacter = null;
            _selectedAccount = null;

            return;
        }
        
        if (!Wizard.TryGetCharacterId(message.BuddyID, out var characterId)) {
            return;
        }

        var persistentCharacter = WizardCollection.GetCharacter(characterId);
        if (persistentCharacter is null) {
            return;
        }

        var selectedAccount = AccountCollection.GetAccount(persistentCharacter.AccountId);
        if (selectedAccount is null) {
            return;
        }

        _selectedCharacter = persistentCharacter;
        _selectedAccount = selectedAccount;
    }

    private bool IsSenderMuted() {
        if (!GetActiveAccount().InfractionHistory.IsCurrentlyMuted) {
            return false;
        }

        InformGameClient("You are currently muted.");

        return true;
    }

    private void SendDirectedChat(ulong targetId, IMessage msg) {
        if (BuddyRelationshipCollection.HasBlocked(targetId, GetActiveWizard().CharId)) {
            return;
        }

        if (!TryGetOnlinePlayer(targetId, out var targetPlayer)) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_DIRECTEDCHATFAIL());

            return;
        }

        Context.ActorSelection(targetPlayer.ActorPath).Tell(msg);
    }

    private void SendRadialChatTo(IEnumerable<ulong> targetIds, ByteString text) {
        if (IsSenderMuted()) {
            return;
        }

        var cleanedMessage = RadialChatText.Clean(text);
        if (cleanedMessage.Length == 0) {
            return;
        }

        var wizard = GetActiveWizard();
        LogChatMessage(wizard.PlayerNameBehavior.GetWizardName(), cleanedMessage, wizard.Zone);
        SaveChatLog(cleanedMessage, wizard);
        foreach (var targetId in targetIds) {
            SendDirectedChat(targetId, new GAME_5_PROTOCOL.MSG_DIRECTEDCHAT {
                SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
                SourceID = wizard.CharId,
                Message = cleanedMessage,
                TargetID = targetId,
                Filter = 0
            });
        }
    }

    private void SendQuickChatTo(IEnumerable<ulong> targetIds, uint messageId) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        foreach (var targetId in targetIds) {
            SendDirectedChat(targetId, new GAME_5_PROTOCOL.MSG_DIRECTEDQUICKCHAT {
                SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
                SourceID = wizard.CharId,
                MessageID = messageId,
                Filter = 0
            });
        }
    }

    private void SendQuickChatExtTo(IEnumerable<ulong> targetIds, ByteString text) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        if (!TryCleanChatLine(text, wizard, out var cleaned)) {
            return;
        }

        foreach (var targetId in targetIds) {
            SendDirectedChat(targetId, new GAME_5_PROTOCOL.MSG_DIRECTEDQUICKCHATEXT {
                SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
                SourceID = wizard.CharId,
                Message = cleaned,
                Filter = 0
            });
        }
    }

    private static bool TryCleanChatLine(ByteString raw, Wizard wizard, out ByteString cleaned) {
        var text = RadialChatText.Clean(raw);
        cleaned = default;
        if (text.Length == 0) {
            return false;
        }

        cleaned = RadialChatText.Encode(text);

        LogChatMessage(wizard.PlayerNameBehavior.GetWizardName(), text, wizard.Zone);
        SaveChatLog(text, wizard);

        return true;
    }

    private void SendGroupChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT message, Wizard wizard) {
        var text = message.Message.ToString() ?? string.Empty;

        GroupDirectory.Instance?.Tell(new GROUP_109_PROTOCOL.MSG_GROUPCHAT {
            SenderCharId = wizard.CharId,
            ChannelId = message.TargetID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Message = message.Message,
            CharIdsIgnoringSender = BuddyRelationshipCollection.GetCharactersWhoBlocked(wizard.CharId)
        });

        Logger.Information("[Group {Channel}] {Name}: {Message}",
            Logger.Args(message.TargetID, wizard.PlayerNameBehavior.GetWizardName(), text));
        SaveChatLog(text, wizard);
    }

    private void SendChatCommand(string input, CoreObject charObj, Wizard wizard) {
        var account = GetActiveAccount();

        _dispatcherRef.Tell(CommandDispatcher.CreateCommand(input[COMMAND_PREFIX.Length..], SessionActor, charObj,
            wizard, account, _selectedCharacter, _selectedAccount));
    }

}