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
 * HIDDEN QUESTS BLOB
 * ========================================================================
 * 
 * PURPOSE:
 * The client's hidden quests blob format: u32 LE magic 0xFACEACE7, u8 version 1, u32 count,
 * count x (u32 quest name hash, u8 flags), then a trailing u8 0.
 * 
 * NOTE:
 * Captured from one live sample with count 0 (E7 AC CE FA 01 00 00 00 00 00). The entry layout and the
 * trailing byte are inferred from that sample and not yet seen with entries.
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Buffers.Binary;
using Imcodec.IO;

namespace Imlight.CoreLib.Game.Services;

internal static class HiddenQuestsBlob {

    private const uint Magic = 0xFACEACE7;
    private const byte Version = 1;
    private const int HeaderBytes = 9;
    private const int EntryBytes = 5;
    private const int TrailerBytes = 1;

    internal static ByteString Empty => new(new byte[] { 0xE7, 0xAC, 0xCE, 0xFA, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 });

    internal static bool IsWellFormed(ReadOnlySpan<byte> bytes) {
        if (bytes.Length < HeaderBytes + TrailerBytes
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic
            || bytes[4] != Version) {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[5..]);
        return (bytes.Length - HeaderBytes - TrailerBytes) / (long) EntryBytes == count
            && (bytes.Length - HeaderBytes - TrailerBytes) % EntryBytes == 0
            && bytes[^1] == 0;
    }

}
