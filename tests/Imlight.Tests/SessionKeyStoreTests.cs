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
 * Behaviour tests for issuing and validating per-login game session keys and transfer tickets.
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

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Imlight.CoreLib.Game;
using Xunit;

namespace Imlight.Tests;

public class SessionKeyStoreTests {

    private const ulong UserId = 42;
    private const string RemoteIp = "192.168.10.121";
    private const string ZoneName = "WizardCity/Interiors/WC_Headmistress_House";
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

        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);
        Assert.Equal((true, true), store.Describe(UserId));
        Assert.Equal((false, false), store.Describe(UserId + 1));
    }

    [Fact]
    public void LoginKey_StillValid_AfterTransferTicketIssued() {
        var store = new SessionKeyStore();
        var loginKey = store.IssueLoginKey(UserId, s_validity);
        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);

        Assert.True(store.Validate(UserId, loginKey));
    }

    [Fact]
    public void NewLoginKey_ClearsTransferTicket() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);
        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);
        _ = store.IssueLoginKey(UserId, s_validity);

        Assert.Equal(SessionKeyStore.AttachVerdict.NoTicket, store.Validate(UserId, [], RemoteIp, ZoneName));
    }

    [Fact]
    public void TransferKey_WrongValue_NoPendingTransfer_Fails() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);

        Assert.False(store.Validate(UserId, DecimalBytes(12345)));
    }

    [Fact]
    public void Ticket_EmptyKey_SameIp_SameZone_AcceptsOnce() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);
        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);

        Assert.Equal(SessionKeyStore.AttachVerdict.TransferTicket, store.Validate(UserId, [], RemoteIp, ZoneName));
        Assert.Equal(SessionKeyStore.AttachVerdict.NoTicket, store.Validate(UserId, [], RemoteIp, ZoneName));
    }

    [Fact]
    public void Ticket_EmptyKey_OtherIp_Refused() {
        var store = new SessionKeyStore();
        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);

        Assert.Equal(SessionKeyStore.AttachVerdict.OtherAddress, store.Validate(UserId, [], "10.0.0.9", ZoneName));
        Assert.Equal(SessionKeyStore.AttachVerdict.TransferTicket, store.Validate(UserId, [], RemoteIp, ZoneName));
    }

    [Fact]
    public void Ticket_EmptyKey_OtherZone_Refused() {
        var store = new SessionKeyStore();
        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);

        Assert.Equal(SessionKeyStore.AttachVerdict.OtherZone, store.Validate(UserId, [], RemoteIp, "WizardCity/WC_Hub"));
    }

    [Fact]
    public void Ticket_NonEmptyWrongKey_Refused() {
        var store = new SessionKeyStore();
        _ = store.IssueLoginKey(UserId, s_validity);
        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);

        Assert.Equal(SessionKeyStore.AttachVerdict.WrongKey, store.Validate(UserId, DecimalBytes(12345), RemoteIp, ZoneName));
    }

    [Fact]
    public void Ticket_Expired_Refused() {
        var store = new SessionKeyStore(TimeSpan.FromMilliseconds(5));
        store.IssueTransferTicket(UserId, RemoteIp, ZoneName);

        Thread.Sleep(50);

        Assert.Equal(SessionKeyStore.AttachVerdict.NoTicket, store.Validate(UserId, [], RemoteIp, ZoneName));
    }

    private static byte[] DecimalBytes(int value)
        => Encoding.UTF8.GetBytes(value.ToString(CultureInfo.InvariantCulture));

}
