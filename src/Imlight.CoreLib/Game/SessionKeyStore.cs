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
 * Issues one random key per user at login and checks the key a game client attaches with.
 * 
 * USAGE EXAMPLE:
 * var key = store.Issue(accountId, validity); store.Validate(userId, attachedKey);
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
using System.Security.Cryptography;
using Imlight.CoreLib.Shared.Structures;

namespace Imlight.CoreLib.Game;

internal sealed class SessionKeyStore {

    private const int KeyLength = 32;

    private readonly Cache<ulong, byte[]> _keys = new();

    public byte[] Issue(ulong userId, TimeSpan validity) {
        var key = RandomNumberGenerator.GetBytes(KeyLength);
        _keys.Store(userId, [.. key], validity);

        return key;
    }

    public bool Validate(ulong userId, ReadOnlySpan<byte> key) {
        var stored = _keys.Get(userId);
        return stored is not null && CryptographicOperations.FixedTimeEquals(stored, key);
    }

    public bool Remove(ulong userId)
        => _keys.Remove(userId);

}
