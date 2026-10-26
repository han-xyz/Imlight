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
 * ADMIN PANEL CROWNS TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Checks the bounds of a staff Crowns change.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * Created by: BobFromIHT with Claude
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imlight.CoreLib.Game.AdminPanel;
using Xunit;

namespace Imlight.Tests;

public class AdminPanelCrownsTests {

    [Theory]
    [InlineData(1000, 2500, 3500)]
    [InlineData(3500, -3500, 0)]
    [InlineData(0, int.MaxValue, int.MaxValue)]
    public void Change_WithinBounds_Applies(int before, int change, int expected) {
        Assert.True(AccountAdministration.TryAdjustCrowns(before, change, out var after));
        Assert.Equal(expected, after);
    }

    [Theory]
    [InlineData(3500, -3501)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(0, int.MinValue)]
    public void Change_OutOfBounds_KeepsBalance(int before, int change) {
        Assert.False(AccountAdministration.TryAdjustCrowns(before, change, out var after));
        Assert.Equal(before, after);
    }

}
