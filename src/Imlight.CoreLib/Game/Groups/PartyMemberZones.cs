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
 * The answers to MSG_PARTYREQUESTMEMBERZONES and MSG_PARTYZONEREQUEST, limited to the requester's own group.
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

using System;
using System.Linq;

namespace Imlight.CoreLib.Game.Groups;

internal static class PartyMemberZones {

    /// <summary>
    /// "charId:zonePath" for each member in roster order, joined by ';'. Members <paramref name="currentZoneOf"/> has
    /// no zone for (offline), and members who block the requester or are blocked by them, are left out.
    /// </summary>
    public static string Format(GroupSnapshot group, ulong requesterCharId, Func<ulong, string> currentZoneOf,
                                Func<ulong, ulong, bool> hasBlocked)
        => string.Join(';', group.Members
            .Where(member => member.CharId == requesterCharId || !AreBlocked(requesterCharId, member.CharId, hasBlocked))
            .Select(member => (member.CharId, Zone: currentZoneOf(member.CharId)))
            .Where(member => !string.IsNullOrEmpty(member.Zone))
            .Select(member => $"{member.CharId}:{member.Zone}"));

    /// <summary>
    /// Whether the requester may learn the target's party: their own character, or a group mate when neither blocks
    /// the other.
    /// </summary>
    public static bool CanQuery(ulong requesterCharId, ulong targetCharId, GroupSnapshot targetGroup,
                                Func<ulong, ulong, bool> hasBlocked) {
        if (targetCharId == requesterCharId) {
            return true;
        }

        return targetGroup is not null
            && targetGroup.HasMember(requesterCharId)
            && targetGroup.HasMember(targetCharId)
            && !AreBlocked(requesterCharId, targetCharId, hasBlocked);
    }

    private static bool AreBlocked(ulong first, ulong second, Func<ulong, ulong, bool> hasBlocked)
        => hasBlocked(first, second) || hasBlocked(second, first);

}
