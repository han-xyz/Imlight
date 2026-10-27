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
 * BANK CAPACITY TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the bank capacities read from Imlight.ini: an empty or zero value is the retail 100.
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

using System.IO;
using Imlight.Common;
using Imlight.CoreLib.Game.Bank;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class BankCapacityTests {

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Capacities_EmptyOrNonPositive_AreRetailDefault(string value) {
        WithCharacterSection($"MaxBankItems = {value}\nMaxSharedBankItems = {value}\n", () => {
            Assert.Equal(100, BankConfig.MaxBankItems);
            Assert.Equal(100, BankConfig.MaxSharedBankItems);
            Assert.True(BankConfig.IsEnabled);
        });
    }

    [Fact]
    public void Capacities_Configured_AreKept() {
        WithCharacterSection("MaxBankItems = 60\nMaxSharedBankItems = 80\n", () => {
            Assert.Equal(60, BankConfig.MaxBankItems);
            Assert.Equal(80, BankConfig.MaxSharedBankItems);
        });
    }

    [Fact]
    public void SharedBankLimit_ZeroItemLimit_FollowsConfig() {
        WithCharacterSection("MaxSharedBankItems = 80\n", () => {
            Assert.Equal(80, new AccountSharedBank { ItemLimit = 0 }.Limit);
            Assert.Equal(25, new AccountSharedBank { ItemLimit = 25 }.Limit);
        });
    }

    private static void WithCharacterSection(string body, System.Action assert) {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Character]\n" + body);
            ConfigurationManager.Initialize(iniFilePath);
            assert();
        } finally {
            File.Delete(iniFilePath);
        }
    }

}
