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
 * NETWORKING
 * ========================================================================
 * 
 * PURPOSE:
 * Radial chat text on the wire: a length-prefixed wide string. Clean drops control characters and payloads shorter
 * than their length prefix, so relayed lines carry only what a chat line may.
 * 
 * USAGE EXAMPLE:
 * ChatService cleans every radial and quick chat line before it is logged and relayed.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System.IO;
using System.Linq;
using Imcodec.IO;
using Imcodec.MessageLayer;

namespace Imlight.CoreLib.Shared.Networking;

internal static class RadialChatText {

    public static string Clean(ByteString payload) {
        if (payload.IsEmpty) {
            return string.Empty;
        }

        try {
            var lengthReader = new BitReader((byte[]) payload);
            var characterCount = lengthReader.ReadUInt16();
            if (lengthReader.GetRelativeData().Length < characterCount * 2) {
                return string.Empty;
            }

            var text = new BitReader((byte[]) payload).ReadWString();

            return new string(text.Where(character => !char.IsControl(character)).ToArray()).Trim();
        }
        catch (EndOfStreamException) {
            return string.Empty;
        }
    }

    public static ByteString Encode(string text) {
        var writer = new BitWriter();
        writer.WriteWString(text);

        return new ByteString(writer.GetData());
    }

}
