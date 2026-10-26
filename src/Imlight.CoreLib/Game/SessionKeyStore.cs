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
 * GAME SERVER SESSION KEYS
 * ========================================================================
 * 
 * PURPOSE:
 * Issues per-user game session keys at login and transfer tickets on a server transfer, and checks
 * the attach a game client makes. One instance is shared by every realm.
 * 
 * USAGE EXAMPLE:
 * var key = store.IssueLoginKey(accountId, validity); store.Validate(userId, attachedKey, remoteIp, zoneName);
 * 
 * NOTE:
 * The client attaches after MSG_SERVERTRANSFER with an empty key, whatever the transfer message carried.
 * An empty-key attach is accepted only on a transfer ticket: same source address as the session that
 * requested the transfer, the destination or fallback zone the server sent, within 60 s, once.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Imlight.CoreLib.Shared.Structures;

namespace Imlight.CoreLib.Game;

public sealed class SessionKeyStore(TimeSpan transferTicketValidity) {

    public enum AttachVerdict {
        LoginKey,
        TransferTicket,
        WrongKey,
        NoTicket,
        OtherAddress,
        OtherZone
    }

    private const int LoginKeyLength = 32;
    private static readonly TimeSpan s_defaultTransferTicketValidity = TimeSpan.FromSeconds(60);

    private readonly Lock _lock = new();
    private readonly Cache<ulong, byte[]> _loginKeys = new();
    private readonly Cache<ulong, Ticket> _transferTickets = new();

    public SessionKeyStore()
        : this(s_defaultTransferTicketValidity) {
    }

    public byte[] IssueLoginKey(ulong userId, TimeSpan validity) {
        // MSG_ATTACH.LoginKey is a STR, so the random bytes travel as Base64 text with no NUL.
        var key = Encoding.ASCII.GetBytes(Convert.ToBase64String(RandomNumberGenerator.GetBytes(LoginKeyLength)));
        lock (_lock) {
            _ = RemoveExpiredEntries();
            _loginKeys.Store(userId, [.. key], validity);
            _ = _transferTickets.Remove(userId);
        }

        return key;
    }

    public int RemoveExpired() {
        lock (_lock) {
            return RemoveExpiredEntries();
        }
    }

    public void IssueTransferTicket(ulong userId, string remoteIp, string zoneName, string fallbackZoneName) {
        lock (_lock) {
            _transferTickets.Store(userId, new Ticket(remoteIp, zoneName, fallbackZoneName), transferTicketValidity);
        }
    }

    public bool Validate(ulong userId, ReadOnlySpan<byte> key)
        => Validate(userId, key, null, null) == AttachVerdict.LoginKey;

    public AttachVerdict Validate(ulong userId, ReadOnlySpan<byte> key, string remoteIp, string zoneName) {
        lock (_lock) {
            if (Matches(_loginKeys.Get(userId), key)) {
                return AttachVerdict.LoginKey;
            }

            if (!key.IsEmpty) {
                return AttachVerdict.WrongKey;
            }

            var ticket = _transferTickets.Get(userId);
            if (ticket is null) {
                return AttachVerdict.NoTicket;
            }

            if (!string.Equals(ticket.RemoteIp, remoteIp, StringComparison.Ordinal)) {
                return AttachVerdict.OtherAddress;
            }

            if (!string.Equals(ticket.ZoneName, zoneName, StringComparison.Ordinal)
                && !string.Equals(ticket.FallbackZoneName, zoneName, StringComparison.Ordinal)) {
                return AttachVerdict.OtherZone;
            }

            _ = _transferTickets.Remove(userId);
            return AttachVerdict.TransferTicket;
        }
    }

    public (bool HasLoginKey, bool HasTransferTicket) Describe(ulong userId) {
        lock (_lock) {
            return (_loginKeys.Get(userId) is not null, _transferTickets.Get(userId) is not null);
        }
    }

    private int RemoveExpiredEntries()
        => RemoveExpired(_loginKeys) + RemoveExpired(_transferTickets);

    private static int RemoveExpired<T>(Cache<ulong, T> cache) where T : class {
        // Cache.Get drops an entry once it has expired.
        return cache.Select(entry => entry.Key).ToArray().Count(userId => cache.Get(userId) is null);
    }

    private static bool Matches(byte[] stored, ReadOnlySpan<byte> key)
        => stored is not null && CryptographicOperations.FixedTimeEquals(stored, key);

    private sealed record Ticket(string RemoteIp, string ZoneName, string FallbackZoneName);

}
