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
 * GROUPS
 * ========================================================================
 * 
 * PURPOSE:
 * The answer to MSG_PARTYREQUESTMEMBERZONES: each online member of a group with the zone they are in.
 * 
 * USAGE EXAMPLE:
 * GroupService sends it as MSG_PARTYSUBMITMEMBERZONES.PartyMemberZones.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System.Linq;

namespace Imlight.CoreLib.Game.Groups;

internal static class PartyMemberZones {

    /// <summary>
    /// "charId:zonePath" for each online member in roster order, joined by ';'.
    /// </summary>
    public static string Format(GroupSnapshot group)
        => string.Join(';', group.Members
            .Where(member => member.IsOnline)
            .Select(member => $"{member.CharId}:{member.ZonePath}"));

}
