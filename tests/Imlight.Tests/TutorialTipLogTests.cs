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
 * Behaviour tests for the per-character log of read tutorial tips and its wire encoding.
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

using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class TutorialTipLogTests {

    [Fact]
    public void ReadTipsRoundTripAsLittleEndianIdsOnce() {
        var wizard = new Wizard { CharId = 1 };

        Assert.True(TutorialTipService.TryRecordReadTip(wizard, 7));
        Assert.True(TutorialTipService.TryRecordReadTip(wizard, 300));
        Assert.False(TutorialTipService.TryRecordReadTip(wizard, 7));

        Assert.Equal(new byte[] { 7, 0, 0, 0, 0x2C, 1, 0, 0 }, (byte[]) TutorialTipService.EncodeTipLog(wizard));
    }

    [Fact]
    public void NoReadTipsEncodeAsAnEmptyLog() {
        Assert.True(TutorialTipService.EncodeTipLog(new Wizard { CharId = 1 }).IsEmpty);
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

}
