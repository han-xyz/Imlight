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
 * ADMIN MODE TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Checks which commands may run while a staff account's admin mode is off.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * NOTE:
 * The account actions themselves need a database; tests/Imlight.AdminPanel.AccountTests covers them.
 *
 * Created by: BobFromIHT with Claude
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Linq;
using System.Reflection;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.Game.Commands.Protocols;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public class AdminModeTests {

    private const BindingFlags CommandBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void OnlyAdminCommands_RunWithoutAdminMode() {
        var withoutAdminMode = typeof(CommandProtocol).Assembly.GetTypes()
            .SelectMany(x => x.GetMethods(CommandBindingFlags))
            .Where(x => x.GetCustomAttribute<WithoutAdminModeAttribute>() is not null)
            .Select(x => x.DeclaringType!)
            .Distinct();

        Assert.Equal([typeof(CommandAdminProtocol)], withoutAdminMode);
    }

    [Fact]
    public void AdminCommands_NeedAStaffLevel() {
        var levels = typeof(CommandAdminProtocol).GetMethods(CommandBindingFlags)
            .Where(x => x.GetCustomAttribute<CommandAttribute>() is not null)
            .Select(x => x.GetCustomAttribute<AuthRequiredAttribute>()?.Level);

        Assert.All(levels, level => Assert.Equal(AuthLevel.QualityAssurance, level));
    }

}
