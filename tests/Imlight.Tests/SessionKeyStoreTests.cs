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
 * SESSION KEY STORE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for issuing and validating per-login game session keys.
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

using System;
using System.Threading;
using Imlight.CoreLib.Game;
using Xunit;

namespace Imlight.Tests;

public class SessionKeyStoreTests {

    private const ulong UserId = 42;
    private static readonly TimeSpan s_validity = TimeSpan.FromHours(8);

    [Fact]
    public void Key_Is32RandomBytes() {
        var store = new SessionKeyStore();

        var first = store.Issue(UserId, s_validity);
        var second = store.Issue(UserId + 1, s_validity);

        Assert.Equal(32, first.Length);
        Assert.Equal(32, second.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Validate_WrongKey_Fails() {
        var store = new SessionKeyStore();
        var key = store.Issue(UserId, s_validity);
        key[0] ^= 0xFF;

        Assert.False(store.Validate(UserId, key));
    }

    [Fact]
    public void Validate_RightKey_Succeeds() {
        var store = new SessionKeyStore();
        var key = store.Issue(UserId, s_validity);

        Assert.True(store.Validate(UserId, key));
    }

    [Fact]
    public void Validate_UnknownUser_Fails() {
        var store = new SessionKeyStore();
        var key = store.Issue(UserId, s_validity);

        Assert.False(store.Validate(UserId + 1, key));
    }

    [Fact]
    public void Validate_Expired_Fails() {
        var store = new SessionKeyStore();
        var key = store.Issue(UserId, TimeSpan.FromMilliseconds(5));

        Thread.Sleep(50);

        Assert.False(store.Validate(UserId, key));
    }

    [Fact]
    public void Validate_Twice_Succeeds() {
        var store = new SessionKeyStore();
        var key = store.Issue(UserId, s_validity);

        Assert.True(store.Validate(UserId, key));
        Assert.True(store.Validate(UserId, key));
    }

    [Fact]
    public void NewKey_ReplacesOld() {
        var store = new SessionKeyStore();
        var oldKey = store.Issue(UserId, s_validity);
        var newKey = store.Issue(UserId, s_validity);

        Assert.False(store.Validate(UserId, oldKey));
        Assert.True(store.Validate(UserId, newKey));
    }

    [Fact]
    public void Validate_WrongLength_Fails() {
        var store = new SessionKeyStore();
        var key = store.Issue(UserId, s_validity);

        Assert.False(store.Validate(UserId, key.AsSpan(0, 16)));
        Assert.False(store.Validate(UserId, ReadOnlySpan<byte>.Empty));
    }

}
