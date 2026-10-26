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
 * HIDDEN QUESTS SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Keeps the quest log's hidden quests per character, so a quest hidden in the log stays hidden after relog.
 * 
 * USAGE EXAMPLE:
 * Internal service attached to SessionActor; the client sends MSG_UPDATEUSERHIDDENQUESTS when a quest is hidden or shown.
 * 
 * NOTE:
 * The client's blob is stored as-is and handed back in HiddenQuestsBehavior.m_hiddenQuestsData on attach
 * (WizardObjectLoader.SetHiddenQuestsBehavior).
 * 
 * TODO:
 * - Check that the blob names only quests the character holds; its format needs a captured payload first.
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */


using System;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal sealed class HiddenQuestsService(SessionActor sessionActor) : MessageService(sessionActor) {

    internal const int MaxHiddenQuestsDataBytes = 8192;

    internal static bool TryStoreHiddenQuests(Wizard wizard, ByteString hiddenQuestsData) {
        if (hiddenQuestsData.Length > MaxHiddenQuestsDataBytes) {
            return false;
        }

        wizard.HiddenQuestsData = hiddenQuestsData;
        if (wizard.GameObject?.m_inactiveBehaviors is not null
            && CoreObjectFactory.FindBehaviorInstance<HiddenQuestsBehavior>(wizard.GameObject, out var hiddenQuestsBehavior)) {
            hiddenQuestsBehavior.m_hiddenQuestsData = hiddenQuestsData;
        }

        return true;
    }

    [MessageHandler(typeof(QUEST_MESSAGES_52_PROTOCOL.MSG_UPDATEUSERHIDDENQUESTS))]
    private void ReceiveHiddenQuestsUpdate(QUEST_MESSAGES_52_PROTOCOL.MSG_UPDATEUSERHIDDENQUESTS message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            Logger.Debug("Session {SessionId} sent hidden quests data with no active wizard; dropping it.",
                Logger.Args(SessionActor.SessionID));

            return;
        }

        if (wizard.HiddenQuestsData == message.Data) {
            return;
        }

        if (!TryStoreHiddenQuests(wizard, message.Data)) {
            Logger.Warning("Rejected hidden quests data from character {Id}: {Length} bytes is over the {Max} byte limit.",
                Logger.Args(wizard.CharId, message.Data.Length, MaxHiddenQuestsDataBytes));
            return;
        }

        // The blob stays opaque until a live payload is captured; its length and first bytes are logged to get the format.
        byte[] blob = message.Data;
        Logger.Debug("Hidden quests blob of character {Id}: {Length} bytes, starts {Head}",
            Logger.Args(wizard.CharId, blob.Length, Convert.ToHexString(blob.AsSpan(0, Math.Min(blob.Length, 16)))));

        WizardCollection.UpdateCharacterHiddenQuests(wizard);
        Logger.Information("Saved hidden quests data for character {Id}", Logger.Args(wizard.CharId));
    }

}
