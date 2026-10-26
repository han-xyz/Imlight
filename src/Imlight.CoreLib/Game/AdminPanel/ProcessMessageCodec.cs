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
 */

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Converts between messages and the payload of MSG_MESSAGE_PROCESS, which carries one DML message
/// (message-layer header and body) without the packet frame.
/// </summary>
internal static class ProcessMessageCodec {

    private const ushort MagicHeader = 0xF00D;
    private const int FrameHeaderLength = 8;
    private const int TrailingNullLength = 1;

    // Larger bodies switch the encoder to a longer frame header.
    private const int MaxSmallPacketBody = 0x777F;

    /// <summary>
    /// Decodes a process message payload. Returns null if it is not exactly one known message.
    /// </summary>
    public static IMessage Decode(byte[] payload) {
        if (payload is null || payload.Length == 0 || payload.Length > ushort.MaxValue - FrameHeaderLength) {
            return null;
        }

        // Rebuild the packet frame the decoder expects: magic, length, control flag, opcode and padding.
        var frame = new byte[FrameHeaderLength + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, MagicHeader);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), (ushort) (payload.Length + FrameHeaderLength));
        payload.CopyTo(frame, FrameHeaderLength);

        // Payloads come from clients, so a malformed one is rejected rather than thrown.
        try {
            var messages = MessageEncoder.Decode(frame);

            return messages?.Count == 1 ? messages.First() : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException
                                       or IndexOutOfRangeException or OverflowException) {
            return null;
        }
    }

    /// <summary>
    /// Encodes a message as a process message payload.
    /// </summary>
    public static byte[] Encode(IMessage message) {
        var packet = MessageEncoder.Encode(message);
        if (packet.Length - FrameHeaderLength - TrailingNullLength > MaxSmallPacketBody) {
            throw new ArgumentException("The message is too large for a process message.", nameof(message));
        }

        return packet[FrameHeaderLength..^TrailingNullLength];
    }

}
