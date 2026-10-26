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
 * CONSOLE SOCKET POLICY TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for who may reach the server console socket.
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
 * Last Updated: 10/26/2026
 */

using System.IO;
using Imlight.CoreLib.Game.AdminPanel;
using Xunit;

namespace Imlight.Tests;

public class ConsoleSocketPolicyTests {

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    [Fact]
    public void Directory_OwnedByServerAndOwnerOnly_Accepted() {
        Assert.True(ConsoleSocketPolicy.IsDirectoryAcceptable(1000, 1000, OwnerOnly));
    }

    [Fact]
    public void Directory_OwnedByAnotherUser_Rejected() {
        Assert.False(ConsoleSocketPolicy.IsDirectoryAcceptable(0, 1000, OwnerOnly));
    }

    [Fact]
    public void Directory_OwnerUnknown_Rejected() {
        Assert.False(ConsoleSocketPolicy.IsDirectoryAcceptable(null, 1000, OwnerOnly));
    }

    [Theory]
    [InlineData(UnixFileMode.GroupRead)]
    [InlineData(UnixFileMode.GroupExecute)]
    [InlineData(UnixFileMode.OtherRead)]
    [InlineData(UnixFileMode.OtherWrite)]
    [InlineData(UnixFileMode.OtherExecute)]
    public void Directory_GroupOrWorldAccessible_Rejected(UnixFileMode extra) {
        Assert.False(ConsoleSocketPolicy.IsDirectoryAcceptable(1000, 1000, OwnerOnly | extra));
    }

    [Fact]
    public void Peer_SameUid_Accepted() {
        Assert.True(ConsoleSocketPolicy.IsPeerAcceptable(1000, 1000, trustAllLocalPeers: false));
    }

    [Fact]
    public void Peer_OtherUid_Rejected() {
        Assert.False(ConsoleSocketPolicy.IsPeerAcceptable(1001, 1000, trustAllLocalPeers: false));
    }

    [Fact]
    public void Peer_OtherUid_RejectedEvenWhenTrusted() {
        Assert.False(ConsoleSocketPolicy.IsPeerAcceptable(1001, 1000, trustAllLocalPeers: true));
    }

    [Fact]
    public void Peer_UidUnavailable_RejectedUnlessTrusted() {
        Assert.False(ConsoleSocketPolicy.IsPeerAcceptable(null, 1000, trustAllLocalPeers: false));
        Assert.True(ConsoleSocketPolicy.IsPeerAcceptable(null, 1000, trustAllLocalPeers: true));
    }

}
