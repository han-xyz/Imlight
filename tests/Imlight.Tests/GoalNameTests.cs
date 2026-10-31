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
 * GOAL NAME TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for the NPC name a talk goal shows: a name the locale keeps whole (Dorothy Gale,
 * or a PvP kiosk's name in another table) is shown whole instead of left blank.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using Imlight.CoreLib.Shared.Resources;
using Xunit;

namespace Imlight.Tests;

public sealed class GoalNameTests {

    [Theory]
    [InlineData("WC-NPCs_00000735")]
    [InlineData("WizardNPC_00000031")]
    public void UnsplitName_IsTheWholeFirstName(string displayName) {
        var (firstName, lastName) = Locale.GetEnglishNameKeys(displayName);

        Assert.Equal(displayName, firstName);
        Assert.Equal("", lastName);
    }

    [Fact]
    public void NoName_StaysEmpty()
        => Assert.Equal(("", ""), Locale.GetEnglishNameKeys(""));

}
