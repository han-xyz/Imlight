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
 * Keeps the tutorial tips a character has read, so a read tip stays read after relog.
 * 
 * USAGE EXAMPLE:
 * Internal service attached to SessionActor; the client sends MSG_READTUTORIALTIP when a tip is read and
 * MSG_REQUESTTUTORIALTIPLOG at login.
 * 
 * NOTE:
 * MSG_DISMISSTUTORIALTIP is the server's message to the client, so it is not handled here.
 * 
 * TODO:
 * - The log is sent as little-endian u32 tip ids; no retail capture of MSG_UPDATETUTORIALTIPLOG confirms the layout.
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Buffers.Binary;
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

    internal static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TutorialTipService(parentActor));

    internal static bool TryRecordReadTip(Wizard wizard, uint tipId) {
        if (wizard.ReadTutorialTips.Count >= MaxReadTips || wizard.ReadTutorialTips.Contains(tipId)) {
            return false;
        }

        wizard.ReadTutorialTips.Add(tipId);
        return true;
    }

    internal static ByteString EncodeTipLog(Wizard wizard) {
        var bytes = new byte[wizard.ReadTutorialTips.Count * sizeof(uint)];
        for (var i = 0; i < wizard.ReadTutorialTips.Count; i++) {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), wizard.ReadTutorialTips[i]);
        }

        return new ByteString(bytes);
    }

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

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_UPDATETUTORIALTIPLOG {
            GlobalID = message.GlobalID,
            TutorialTipData = EncodeTipLog(wizard)
        });
    }

}
