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
 * AUTHORITY TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for moderation target authority and command auth declarations.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.Linq;
using System.Reflection;
using Imlight.CoreLib.Auth;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.Game.Commands.Protocols;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public class AuthorityTests {

    private const string ProtocolsNamespace = "Imlight.CoreLib.Game.Commands.Protocols";
    private const BindingFlags CommandBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void HallMonitor_CannotActOnAdmin()
        => Assert.False(AuthorityRequester.HasAuthorityOver(AuthLevel.HallMonitor, AuthLevel.Administrator));

    [Fact]
    public void Admin_CanActOnHallMonitor()
        => Assert.True(AuthorityRequester.HasAuthorityOver(AuthLevel.Administrator, AuthLevel.HallMonitor));

    [Fact]
    public void Equal_HallMonitor_Denied()
        => Assert.False(AuthorityRequester.HasAuthorityOver(AuthLevel.HallMonitor, AuthLevel.HallMonitor));

    [Fact]
    public void Equal_Admin_Allowed()
        => Assert.True(AuthorityRequester.HasAuthorityOver(AuthLevel.Administrator, AuthLevel.Administrator));

    [Fact]
    public void None_CannotActOnAnyone() {
        foreach (var target in System.Enum.GetValues<AuthLevel>()) {
            Assert.False(AuthorityRequester.HasAuthorityOver(AuthLevel.None, target));
        }
    }

    [Fact]
    public void Infractions_RequiresHallMonitor() {
        var method = typeof(CommandAccountProtocol).GetMethods(CommandBindingFlags)
            .Single(x => x.GetCustomAttribute<CommandAttribute>()?.Name == "infractions");

        Assert.Equal(AuthLevel.HallMonitor, method.GetCustomAttribute<AuthRequiredAttribute>()?.Level);
    }

    [Fact]
    public void CommandWithoutAuthRequired_Denied() {
        var commandsWithoutAuth = typeof(CommandProtocol).Assembly.GetTypes()
            .Where(x => x.Namespace == ProtocolsNamespace)
            .SelectMany(x => x.GetMethods(CommandBindingFlags))
            .Where(x => x.GetCustomAttribute<CommandAttribute>() is not null)
            .Where(x => x.GetCustomAttribute<AuthRequiredAttribute>() is null)
            .Select(x => $"{x.DeclaringType!.Name}.{x.Name}");

        Assert.Empty(commandsWithoutAuth);
    }

}
