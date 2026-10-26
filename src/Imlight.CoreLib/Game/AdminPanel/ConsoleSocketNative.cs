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
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Imlight.CoreLib.Game.AdminPanel;

internal static class ConsoleSocketNative {

    private const int SolSocket = 1;
    private const int SoPeerCred = 17;
    private const int AtFdCwd = -100;
    private const uint StatxUid = 0x8;
    private const int StatxBufferSize = 256;
    private const int StatxUidOffset = 20;

    [StructLayout(LayoutKind.Sequential)]
    private struct UCred {
        public int Pid;
        public uint Uid;
        public uint Gid;
    }

    public static uint ServerUid => OperatingSystem.IsLinux() ? geteuid() : uint.MaxValue;

    public static uint? GetOwnerUid(string path) {
        if (!OperatingSystem.IsLinux()) {
            return null;
        }

        var buffer = new byte[StatxBufferSize];

        return statx(AtFdCwd, path, 0, StatxUid, buffer) == 0 ? BitConverter.ToUInt32(buffer, StatxUidOffset) : null;
    }

    public static uint? GetPeerUid(Socket socket) {
        if (!OperatingSystem.IsLinux()) {
            return null;
        }

        var credentials = new UCred();
        var length = (uint) Marshal.SizeOf<UCred>();

        return getsockopt((int) socket.Handle, SolSocket, SoPeerCred, ref credentials, ref length) == 0
            ? credentials.Uid
            : null;
    }

    [DllImport("libc")]
    private static extern uint geteuid();

    [DllImport("libc", SetLastError = true)]
    private static extern int statx(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, byte[] buffer);

    [DllImport("libc", SetLastError = true)]
    private static extern int getsockopt(int fd, int level, int optname, ref UCred value, ref uint length);

}
