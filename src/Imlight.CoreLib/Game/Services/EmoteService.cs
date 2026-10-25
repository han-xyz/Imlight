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
 * EMOTE SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player emote interactions and synchronizes emote animations and
 * text messages to other players within the active zone.
 * 
 * USAGE EXAMPLE:
 * Internal service attached to SessionActor for handling emote-related
 * network messages.
 * 
 * NOTE:
 * - Originator players predictively play their emote locally (ExcludeOriginator = 1).
 * - Broadcasts MSG_PIIRADIALMENUPLAYEMOTE (Service 56, Order 73) selflessly
 *   to other players in the zone.
 * 
 * Created by: Revive101
 * Version: KALI 1.0
 * Last Updated: 10/19/2026
 */

using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using System;
using System.Linq;

namespace Imlight.CoreLib.Game.Services;

internal class EmoteService(SessionActor sessionActor) : MessageService(sessionActor) {

    // A radial menu slot holds an emote or a chat phrase; no menu needs more slots than a player may own emotes.
    private static readonly uint s_maxEmotes
        = ConfigurationManager.Settings["Character.MaxEmotesAllowed"].AsUInt();

    private const int MaxAnimationNameLength = 64;

    private static readonly uint ResetStateId = StringHash.Compute("Unremarkable");
    private static readonly uint EmoteStateId = StringHash.Compute("ActionEmoting");

    private DateTime _lastEmoteTime = DateTime.MinValue;
    private string _lastEmoteAnimation = string.Empty;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new EmoteService(parentActor));

    /// <summary>
    /// Handles radial menu emote play requests from the client.
    /// Broadcasts the emote animation and speech text to other players in the zone.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTPIIRADIALMENUPLAYEMOTE))]
    private void ReceiveRequestPIIRadialMenuPlayEmote(WIZARD3_56_PROTOCOL.MSG_REQUESTPIIRADIALMENUPLAYEMOTE message) {
        if (!MayPlay(message.EmoteAnimationName)) {
            return;
        }

        _lastEmoteTime = DateTime.UtcNow;
        _lastEmoteAnimation = message.EmoteAnimationName;

        var wizard = GetActiveWizard();
        var emoteText = EmoteTextForBroadcast(message.EmoteText, GetActiveAccount().InfractionHistory.IsCurrentlyMuted);
        var broadcastMsg = new WIZARD3_56_PROTOCOL.MSG_PIIRADIALMENUPLAYEMOTE {
            SourceID = wizard.GameObjectID,
            EmoteAnimationName = message.EmoteAnimationName,
            EmoteText = emoteText
        };

        if (emoteText.Length == 0) {
            ZoneBroadcast(broadcastMsg);
        }
        else {
            // The text is chat, so it takes the chat path: logged, and hidden from players who blocked the sender.
            LogChatMessage(wizard.PlayerNameBehavior.GetWizardName(), emoteText, wizard.Zone);
            SaveChatLog(emoteText, GetActiveGameObject(), wizard);
            SendFilteredZoneMessage(broadcastMsg, wizard.CharId, wizard.Zone);
        }

        BroadcastEmoteAnimation(wizard.GameObjectID, message.EmoteAnimationName, true);
    }

    private bool MayPlay(string animation) {
        if (!IsValidAnimationName(animation)) {
            Logger.Debug("[EmoteService] Character {Id} sent an invalid emote animation name.",
                Logger.Args(GetActiveWizard().CharId));
            return false;
        }

        if (!CrownShopHandler.TryGetSoldEmote(animation, out var emote) || Owns(emote)) {
            return true;
        }

        Logger.Warning("[EmoteService] Character {Id} played emote {Anim} without owning it.",
            Logger.Args(GetActiveWizard().CharId, animation));
        return false;
    }

    internal static bool IsValidAnimationName(string animation)
        => animation is not null
            && animation.Length <= MaxAnimationNameLength
            && animation.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private bool MayPlace(uint emoteTemplateId)
        => !CrownShopHandler.TryGetSoldEmote(emoteTemplateId, out var emote) || Owns(emote);

    private bool Owns(CustomEmoteBehaviorTemplate emote) {
        var stats = GetActiveWizard().GameStats;
        return CustomEmoteOwnership.Owns(emote, stats.m_purchasedCustomEmotes1, stats.m_purchasedCustomEmotes2, stats.m_purchasedCustomEmotes3);
    }

    internal static string EmoteTextForBroadcast(string emoteText, bool isMuted)
        => isMuted || string.IsNullOrEmpty(emoteText) ? string.Empty : CleanChatText(emoteText);

    /// <summary>
    /// Handles core radial menu emote requests. If not already broadcast via
    /// <see cref="WIZARD3_56_PROTOCOL.MSG_REQUESTPIIRADIALMENUPLAYEMOTE"/>, synchronizes to zone.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_CORE_PIIRADIALMENUEMOTE))]
    private void ReceiveCorePIIRadialMenuEmote(WIZARD3_56_PROTOCOL.MSG_CORE_PIIRADIALMENUEMOTE message) {
        if (!MayPlay(message.EmoteAnimationName)) {
            return;
        }

        // Prevent duplicate broadcast if MSG_REQUESTPIIRADIALMENUPLAYEMOTE was just handled.
        if (string.Equals(_lastEmoteAnimation, message.EmoteAnimationName, StringComparison.Ordinal)
&& (DateTime.UtcNow - _lastEmoteTime) < TimeSpan.FromSeconds(1)) {
            return;
        }


        _lastEmoteTime = DateTime.UtcNow;
        _lastEmoteAnimation = message.EmoteAnimationName;

        var wizard = GetActiveWizard();
        var broadcastMsg = new WIZARD3_56_PROTOCOL.MSG_PIIRADIALMENUPLAYEMOTE {
            SourceID = wizard.GameObjectID,
            EmoteAnimationName = message.EmoteAnimationName,
            EmoteText = string.Empty
        };

        ZoneBroadcast(broadcastMsg, isSelfless: true, targets: ZoneBroadcastTarget.Players);
        BroadcastEmoteAnimation(wizard.GameObjectID, message.EmoteAnimationName, isSelfless: message.ExcludeOriginator == 1);
    }

    /// <summary>
    /// Handles classic core emote messages from GAME_5_PROTOCOL.
    /// </summary>
    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CORE_EMOTE))]
    private void ReceiveCoreEmote(GAME_5_PROTOCOL.MSG_CORE_EMOTE message) {
        if (!MayPlay(message.Name)) {
            return;
        }

        _lastEmoteTime = DateTime.UtcNow;
        _lastEmoteAnimation = message.Name;

        var wizard = GetActiveWizard();
        var broadcastMsg = new WIZARD3_56_PROTOCOL.MSG_PIIRADIALMENUPLAYEMOTE {
            SourceID = wizard.GameObjectID,
            EmoteAnimationName = message.Name,
            EmoteText = string.Empty
        };

        ZoneBroadcast(broadcastMsg);
        BroadcastEmoteAnimation(wizard.GameObjectID, message.Name, message.ExcludeOriginator == 1);
    }

    /// <summary>
    /// Broadcasts MSG_ENTERSTATE with EmoteStateOverrideInfo.
    /// Resets the state machine via "Unremarkable" first so consecutive emotes always trigger.
    /// </summary>
    private void BroadcastEmoteAnimation(ulong gameObjectId, string emoteAnimationName, bool isSelfless) {
        if (string.IsNullOrWhiteSpace(emoteAnimationName)) {
            return;
        }

        var wizard = GetActiveWizard();
        if (wizard.CurrentAnimationState == EmoteStateId) {
            var resetMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
                GameObjectID = gameObjectId,
                State = ResetStateId,
                Data = new ByteString(),
                IgnoreIfCurrentStateIsOff = 0,
            };
            ZoneBroadcast(resetMsg, isSelfless: isSelfless);
        }

        var stateEmote = new EmoteStateOverrideInfo {
            m_stateNameID = EmoteStateId,
            m_emoteName = emoteAnimationName,
            m_particleAsset = string.Empty,
            m_loop = false,
            m_particleNode = string.Empty,
            m_soundAsset = string.Empty,
            m_wizBangID = 0
        };

        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );

        if (!serializer.Serialize(stateEmote, 1, out var emoteData)) {
            Logger.Error("Failed to serialize EmoteStateOverrideInfo for {Anim}",
                Logger.Args(emoteAnimationName));

            return;
        }

        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = gameObjectId,
            State = EmoteStateId,
            Data = emoteData,
            IgnoreIfCurrentStateIsOff = 0,
        }, isSelfless);

        wizard.CurrentAnimationState = EmoteStateId;
    }

    /// <summary>
    /// Handles radial menu layout updates from client. The client sends this message, when the radial menu is closed.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_PIIUPDATEEMOTESRADIALMENU))]
    private void ReceivePIIUpdateEmotesRadialMenu(WIZARD3_56_PROTOCOL.MSG_PIIUPDATEEMOTESRADIALMENU message) {
        var wizard = GetActiveWizard();
        if (!CustomEmoteOwnership.TryAcceptRadialMenu(message.RadialMenuData, (int) s_maxEmotes, MayPlace, out var menu1) ||
            !CustomEmoteOwnership.TryAcceptRadialMenu(message.RadialMenu2Data, (int) s_maxEmotes, MayPlace, out var menu2) ||
            !CustomEmoteOwnership.TryAcceptRadialMenu(message.RadialMenu3Data, (int) s_maxEmotes, MayPlace, out var menu3)) {
            Logger.Warning("[EmoteService] Rejected radial menu data from character {Id}: malformed, too many slots, or an emote not owned.",
                Logger.Args(wizard.CharId));
            return;
        }
        // Skip DB write if nothing changed
        if (wizard.RadialMenuData == menu1 &&
            wizard.RadialMenu2Data == menu2 &&
            wizard.RadialMenu3Data == menu3) {
            return;
        }

        wizard.RadialMenuData = menu1;
        wizard.RadialMenu2Data = menu2;
        wizard.RadialMenu3Data = menu3;
        WizardCollection.UpdateCharacterRadialMenuData(wizard);
        Logger.Information("[EmoteService] Saved updated radial menu data for character {Id}",
            Logger.Args(wizard.CharId));
    }

    /// <summary>
    /// Handles client request to convert emotes to the PII system.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTONDEMANDCONVERTEMOTESTOPIISYSTEM))]
    private void ReceiveRequestOnDemandConvertEmotes(WIZARD3_56_PROTOCOL.MSG_REQUESTONDEMANDCONVERTEMOTESTOPIISYSTEM message) {
        // Client requesting on-demand conversion.
    }

    /// <summary>
    ///  Loads the radial menu from DB
    /// </summary>
    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();

        var updateMsg = new WIZARD3_56_PROTOCOL.MSG_PIIUPDATEEMOTESRADIALMENU {
            RadialMenuData = wizard.RadialMenuData.IsEmpty ? RadialMenuData.Empty : wizard.RadialMenuData,
            RadialMenu2Data = wizard.RadialMenu2Data.IsEmpty ? RadialMenuData.Empty : wizard.RadialMenu2Data,
            RadialMenu3Data = wizard.RadialMenu3Data.IsEmpty ? RadialMenuData.Empty : wizard.RadialMenu3Data
        };
        SendToSocket(updateMsg);

        Logger.Information("[EmoteService] Restored radial menu data to client for character {Id}",
            Logger.Args(wizard.CharId));
    }
}
