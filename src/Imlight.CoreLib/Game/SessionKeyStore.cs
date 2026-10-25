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
 * Issues per-user game session keys (one at login, one per realm transfer) and checks
 * the key a game client attaches with. One instance is shared by every realm.
 * 
 * USAGE EXAMPLE:
 * var key = store.IssueLoginKey(accountId, validity); store.Validate(userId, attachedKey);
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
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Imlight.CoreLib.Shared.Structures;

namespace Imlight.CoreLib.Game;

public sealed class SessionKeyStore(TimeSpan transferKeyValidity) {

    private const int LoginKeyLength = 32;
    private static readonly TimeSpan s_defaultTransferKeyValidity = TimeSpan.FromSeconds(60);

    private readonly Lock _lock = new();
    private readonly Cache<ulong, byte[]> _loginKeys = new();
    private readonly Cache<ulong, byte[]> _transferKeys = new();

    public SessionKeyStore()
        : this(s_defaultTransferKeyValidity) {
    }

    public byte[] IssueLoginKey(ulong userId, TimeSpan validity) {
        // MSG_ATTACH.LoginKey is a STR, so the random bytes travel as Base64 text with no NUL.
        var key = Encoding.ASCII.GetBytes(Convert.ToBase64String(RandomNumberGenerator.GetBytes(LoginKeyLength)));
        lock (_lock) {
            _loginKeys.Store(userId, [.. key], validity);
            _ = _transferKeys.Remove(userId);
        }

        return key;
    }

    public int IssueTransferKey(ulong userId) {
        // MSG_SERVERTRANSFER carries the key as a 32-bit INT; the client attaches with it as a string.
        var key = RandomNumberGenerator.GetInt32(1, int.MaxValue);
        var keyBytes = Encoding.UTF8.GetBytes(key.ToString(CultureInfo.InvariantCulture));
        lock (_lock) {
            _transferKeys.Store(userId, keyBytes, transferKeyValidity);
        }

        return key;
    }

    public bool Validate(ulong userId, ReadOnlySpan<byte> key) {
        lock (_lock) {
            var isLoginKey = Matches(_loginKeys.Get(userId), key);
            var isTransferKey = Matches(_transferKeys.Get(userId), key);
            if (isTransferKey) {
                _ = _transferKeys.Remove(userId);
            }

            return isLoginKey || isTransferKey;
        }
    }

    private static bool Matches(byte[] stored, ReadOnlySpan<byte> key)
        => stored is not null && CryptographicOperations.FixedTimeEquals(stored, key);

}
