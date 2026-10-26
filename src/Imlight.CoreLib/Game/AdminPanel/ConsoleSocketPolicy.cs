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

using System.IO;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Decides who may reach the server console socket: only the server's own user, through a directory only that
/// user can enter.
/// </summary>
public static class ConsoleSocketPolicy {

    private const UnixFileMode GroupAndOtherAccess =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    public static bool IsDirectoryAcceptable(uint? ownerUid, uint serverUid, UnixFileMode mode)
        => ownerUid == serverUid && (mode & GroupAndOtherAccess) == 0;

    public static bool IsPeerAcceptable(uint? peerUid, uint serverUid, bool trustAllLocalPeers)
        => peerUid is { } uid ? uid == serverUid : trustAllLocalPeers;

}
