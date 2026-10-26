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
 * SIGIL COUNTDOWN
 * ========================================================================
 * 
 * PURPOSE:
 * Starts the countdown of a player standing on a sigil.
 * 
 * USAGE EXAMPLE:
 * SigilCountdown.Start(entry, charId, DungeonRunBook.Shared.Wait, StartSigilTimer);
 * 
 * NOTE:
 * - A minigame sigil (SigilRef set) keeps its own occupants, so it stays out of the dungeon runs.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// Starts the countdown of a player standing on a sigil.
/// </summary>
internal static class SigilCountdown {

    /// <summary>
    /// Records a dungeon sigil's wait in the run book, then starts the countdown timer once.
    /// </summary>
    /// <param name="entry">The sigil the player stands on.</param>
    /// <param name="charId">The player.</param>
    /// <param name="waitInRunBook">Records the player waiting for the sigil's destination.</param>
    /// <param name="startTimer">Starts the countdown timer.</param>
    internal static void Start(ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY entry, ulong charId, Action<ulong, string> waitInRunBook,
                               Action<ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY> startTimer) {
        if (entry.SigilRef is null) {
            waitInRunBook(charId, entry.DestinationZone);
        }

        startTimer(entry);
    }

}
