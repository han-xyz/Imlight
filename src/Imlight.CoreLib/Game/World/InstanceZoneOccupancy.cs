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
 * INSTANCE ZONE OCCUPANCY
 * ========================================================================
 * 
 * PURPOSE:
 * Lets an instance container refuse to drop a zone copy that wizards are in, whoever asks for the reset.
 * 
 * USAGE EXAMPLE:
 * occupancy.Report(zoneName, hasPlayers: true);
 * if (!occupancy.MayDrop(zoneName)) { return; }
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// The zones of an instance container that have wizards in them, as their zone actors report it.
/// </summary>
internal sealed class InstanceZoneOccupancy {

    private readonly HashSet<string> _occupiedZones = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records whether a zone has wizards in it.
    /// </summary>
    internal void Report(string zoneName, bool hasPlayers) {
        if (hasPlayers) {
            _occupiedZones.Add(zoneName);
        } else {
            _occupiedZones.Remove(zoneName);
        }
    }

    /// <summary>
    /// Checks whether a zone copy may be dropped: never while a wizard is in it.
    /// </summary>
    internal bool MayDrop(string zoneName) => !_occupiedZones.Contains(zoneName);

}
