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
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Globalization;
using System.Text;
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

        var first = store.IssueLoginKey(UserId, s_validity);
        var second = store.IssueLoginKey(UserId + 1, s_validity);

        foreach (var key in new[] { first, second }) {
            Assert.Equal(44, key.Length);
            Assert.DoesNotContain((byte) 0, key);
            Assert.All(key, b => Assert.True(b < 0x80));
            Assert.Equal(32, Convert.FromBase64String(Encoding.ASCII.GetString(key)).Length);
        }

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Validate_WrongKey_Fails() {
        var store = new SessionKeyStore();
        var key = store.IssueLoginKey(UserId, s_validity);
        key[0] ^= 0xFF;

        Assert.False(store.Validate(UserId, key));
    }

    [Fact]
    public void Validate_RightKey_Succeeds() {
        var store = new SessionKeyStore();
        var key = store.IssueLoginKey(UserId, s_validity);

        Assert.True(store.Validate(UserId, key));
    }

    [Fact]
    public void Validate_UnknownUser_Fails() {
        var store = new SessionKeyStore();
        var key = store.IssueLoginKey(UserId, s_validity);

        Assert.False(store.Validate(UserId + 1, key));
    }

    [Fact]
    public void Validate_Expired_Fails() {
        var store = new SessionKeyStore();
        var key = store.IssueLoginKey(UserId, TimeSpan.FromMilliseconds(5));

        Thread.Sleep(50);

        Assert.False(store.Validate(UserId, key));
    }

    [Fact]
    public void Validate_Twice_Succeeds() {
        var store = new SessionKeyStore();
        var key = store.IssueLoginKey(UserId, s_validity);

        Assert.True(store.Validate(UserId, key));
        Assert.True(store.Validate(UserId, key));
    }

    [Fact]
    public void NewKey_ReplacesOld() {
        var store = new SessionKeyStore();
        var oldKey = store.IssueLoginKey(UserId, s_validity);
        var newKey = store.IssueLoginKey(UserId, s_validity);

        Assert.False(store.Validate(UserId, oldKey));
        Assert.True(store.Validate(UserId, newKey));
    }

    [Fact]
    public void Validate_WrongLength_Fails() {
        var store = new SessionKeyStore();
        var key = store.IssueLoginKey(UserId, s_validity);

        Assert.False(store.Validate(UserId, key.AsSpan(0, 16)));
        Assert.False(store.Validate(UserId, ReadOnlySpan<byte>.Empty));
    }


    [Fact]
    public void SessionKeyStore_Describe_ReportsPresence() {
        var store = new SessionKeyStore();
        Assert.Equal((false, false), store.Describe(UserId));

        _ = store.IssueLoginKey(UserId, s_validity);
        Assert.Equal((true, false), store.Describe(UserId));

        _ = store.IssueTransferKey(UserId);
        Assert.Equal((true, true), store.Describe(UserId));
        Assert.Equal((false, false), store.Describe(UserId + 1));
    }

    [Fact]
    public void Validate_PendingTransfer_WrongKey_AcceptsOnce() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);
        _ = store.IssueTransferKey(UserId);
        var wrongKey = Encoding.ASCII.GetBytes("not-the-key\0");

        Assert.True(store.Validate(UserId, wrongKey));
        Assert.False(store.Validate(UserId, wrongKey));
    }

    [Fact]
    public void TransferKey_DecimalString_Validates() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);
        var transferKey = store.IssueTransferKey(UserId);

        Assert.True(store.Validate(UserId, DecimalBytes(transferKey)));
    }

    [Fact]
    public void LoginKey_StillValid_AfterTransferKeyIssued() {
        var store = new SessionKeyStore();
        var loginKey = store.IssueLoginKey(UserId, s_validity);
        _ = store.IssueTransferKey(UserId);

        Assert.True(store.Validate(UserId, loginKey));
    }

    [Fact]
    public void NewLoginKey_ClearsTransferKey() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);
        var transferKey = store.IssueTransferKey(UserId);
        _ = store.IssueLoginKey(UserId, s_validity);

        Assert.False(store.Validate(UserId, DecimalBytes(transferKey)));
    }

    [Fact]
    public void TransferKey_WrongValue_NoPendingTransfer_Fails() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);

        Assert.False(store.Validate(UserId, DecimalBytes(12345)));
    }

    [Fact]
    public void TransferKey_SingleUse() {
        var store = new SessionKeyStore();
        var transferKey = store.IssueTransferKey(UserId);

        Assert.True(store.Validate(UserId, DecimalBytes(transferKey)));
        Assert.False(store.Validate(UserId, DecimalBytes(transferKey)));
    }

    [Fact]
    public void TransferKey_ExpiresFast() {
        var store = new SessionKeyStore(TimeSpan.FromMilliseconds(5));
        var transferKey = store.IssueTransferKey(UserId);

        Thread.Sleep(50);

        Assert.False(store.Validate(UserId, DecimalBytes(transferKey)));
    }

    private static byte[] DecimalBytes(int value)
        => Encoding.UTF8.GetBytes(value.ToString(CultureInfo.InvariantCulture));

}
