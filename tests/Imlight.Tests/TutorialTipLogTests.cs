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
 * TUTORIAL TIP LOG TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the per-character tutorial tip log: read tip ids, the raw client blob, and the reply.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using Imcodec.IO;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class TutorialTipLogTests {

    [Fact]
    public void ReadTipsAreKeptOnce() {
        var wizard = new Wizard { CharId = 1 };

        Assert.True(TutorialTipService.TryRecordReadTip(wizard, 7));
        Assert.True(TutorialTipService.TryRecordReadTip(wizard, 300));
        Assert.False(TutorialTipService.TryRecordReadTip(wizard, 7));

        Assert.Equal([7U, 300U], wizard.ReadTutorialTips);
    }

    [Fact]
    public void TipsPastTheCapAreRefused() {
        var wizard = new Wizard { CharId = 1 };
        for (uint tip = 0; tip < TutorialTipService.MaxReadTips; tip++) {
            Assert.True(TutorialTipService.TryRecordReadTip(wizard, tip));
        }

        Assert.False(TutorialTipService.TryRecordReadTip(wizard, TutorialTipService.MaxReadTips));
        Assert.Equal(TutorialTipService.MaxReadTips, wizard.ReadTutorialTips.Count);
    }

    [Fact]
    public void TheLogReplyIsEmptyAndAddressedToThePlayerObject() {
        var wizard = new Wizard { CharId = 1 };
        TutorialTipService.TryRecordReadTip(wizard, 7);

        var reply = TutorialTipService.TipLogReply(wizard);

        Assert.True(reply.TutorialTipData.IsEmpty);
        Assert.Equal(wizard.GameObjectID, reply.GlobalID);
    }

    [Fact]
    public void ClientTipLogIsStoredRawUpToTheCap() {
        var wizard = new Wizard { CharId = 1 };
        var atCap = new ByteString(new byte[TutorialTipService.MaxTipLogBytes]);

        Assert.True(TutorialTipService.TryStoreTipLog(wizard, atCap));
        Assert.Equal(atCap, wizard.TutorialTipData);
        Assert.False(TutorialTipService.TryStoreTipLog(wizard, new ByteString(new byte[TutorialTipService.MaxTipLogBytes + 1])));
        Assert.Equal(atCap, wizard.TutorialTipData);
    }

}
