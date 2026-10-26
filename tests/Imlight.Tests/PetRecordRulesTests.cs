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
 */

using Imlight.CoreLib.Game.Bank;
using Imlight.CoreLib.Game.Pet;
using Xunit;

namespace Imlight.Tests;

public sealed class PetRecordRulesTests {

    [Theory]
    [InlineData(true, (int) BankContainer.Backpack, (int) BankContainer.SharedBank, true)]
    [InlineData(false, (int) BankContainer.Backpack, (int) BankContainer.SharedBank, false)]
    [InlineData(true, (int) BankContainer.Bank, (int) BankContainer.SharedBank, true)]
    [InlineData(true, (int) BankContainer.Backpack, (int) BankContainer.Bank, false)]
    [InlineData(true, (int) BankContainer.Bank, (int) BankContainer.Backpack, false)]
    [InlineData(true, (int) BankContainer.SharedBank, (int) BankContainer.Backpack, false)]
    [InlineData(true, (int) BankContainer.SharedBank, (int) BankContainer.Bank, false)]
    public void DropsRecordOnMove_OnlyAPetDepositedIntoTheSharedBank(bool isPet, int from, int to, bool expected) {
        Assert.Equal(expected, PetRecordRules.DropsRecordOnMove(isPet, (BankContainer) from, (BankContainer) to));
    }

}
