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
 * TUTORIAL TIP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Keeps the tutorial tips a character has read and the tip log blob the client sends, per character.
 * 
 * USAGE EXAMPLE:
 * Internal service attached to SessionActor; the client sends MSG_READTUTORIALTIP when a tip is read and
 * MSG_REQUESTTUTORIALTIPLOG at login.
 * 
 * NOTE:
 * MSG_DISMISSTUTORIALTIP is the server's message to the client, so it is not handled here.
 * The log reply stays empty until a capture of MSG_UPDATETUTORIALTIPLOG or TutorialLogBehavior.m_tutorialTipData
 * shows its layout; the client's own blob is kept raw for that capture.
 * 
 * TODO:
 * - Is the log a bit array sized by MSG_REQUESTTUTORIALTIPLOG's TutorialTipCount?
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal sealed class TutorialTipService(SessionActor sessionActor) : MessageService(sessionActor) {

    internal const int MaxReadTips = 1024;
    internal const int MaxTipLogBytes = 4096;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TutorialTipService(parentActor));

    internal static bool TryRecordReadTip(Wizard wizard, uint tipId) {
        if (wizard.ReadTutorialTips.Count >= MaxReadTips || wizard.ReadTutorialTips.Contains(tipId)) {
            return false;
        }

        wizard.ReadTutorialTips.Add(tipId);
        return true;
    }

    internal static bool TryStoreTipLog(Wizard wizard, ByteString tipLogData) {
        if (tipLogData.Length > MaxTipLogBytes) {
            return false;
        }

        wizard.TutorialTipData = tipLogData;
        return true;
    }

    internal static WIZARD2_53_PROTOCOL.MSG_UPDATETUTORIALTIPLOG TipLogReply(Wizard wizard) => new() {
        GlobalID = wizard.GameObjectID,
        TutorialTipData = new ByteString()
    };

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_READTUTORIALTIP))]
    private void ReceiveReadTutorialTip(WIZARD2_53_PROTOCOL.MSG_READTUTORIALTIP message) {
        var wizard = GetActiveWizard();
        if (wizard is null || !TryRecordReadTip(wizard, message.TutorialTipID)) {
            return;
        }

        WizardCollection.UpdateCharacterTutorialTips(wizard);
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTTUTORIALTIPLOG))]
    private void ReceiveTutorialTipLogRequest(WIZARD2_53_PROTOCOL.MSG_REQUESTTUTORIALTIPLOG message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        SendToSocket(TipLogReply(wizard));
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_UPDATETUTORIALTIPLOG))]
    private void ReceiveTutorialTipLogUpdate(WIZARD2_53_PROTOCOL.MSG_UPDATETUTORIALTIPLOG message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        if (!TryStoreTipLog(wizard, message.TutorialTipData)) {
            Logger.Warning("Rejected tutorial tip log from character {Id}: {Length} bytes is over the {Max} byte limit.",
                Logger.Args(wizard.CharId, message.TutorialTipData.Length, MaxTipLogBytes));
            return;
        }

        byte[] blob = message.TutorialTipData;
        Logger.Debug("Tutorial tip log of character {Id}: {Length} bytes, starts {Head}",
            Logger.Args(wizard.CharId, blob.Length, Convert.ToHexString(blob.AsSpan(0, Math.Min(blob.Length, 16)))));

        WizardCollection.UpdateCharacterTutorialTips(wizard);
    }

}
