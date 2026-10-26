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
 * SIGIL ENTRY PLAN
 * ========================================================================
 * 
 * PURPOSE:
 * Decides whose instance a wizard enters from a dungeon sigil, and whether that entry starts a fresh run.
 * 
 * USAGE EXAMPLE:
 * var entry = SigilEntryPlan.Decide(charId, group, padOccupants, liveOwnerCharId);
 * 
 * NOTE:
 * - Grouped wizards share the instance of the group leader when the leader is on the pad, otherwise of the first
 *   member to enter. Only the first entrant resets it; members who enter later join it as it is.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Game.Groups;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// The instance a sigil entrant is sent to.
/// </summary>
/// <param name="OwnerCharId">The owner of the instance container the entrant joins.</param>
/// <param name="ResetInstance">Whether the entry drops the loaded copy and starts a fresh run.</param>
internal sealed record SigilEntry(ulong OwnerCharId, bool ResetInstance);

internal static class SigilEntryPlan {

    /// <summary>
    /// Decides the instance a wizard enters when their sigil countdown ends.
    /// </summary>
    /// <param name="entrantCharId">The wizard whose countdown ended.</param>
    /// <param name="group">The entrant's group, or null when they are not grouped.</param>
    /// <param name="padOccupants">The members of the group still counting down on the pad, the entrant included.</param>
    /// <param name="liveOwnerCharId">The owner of the group's instance of this dungeon while one is running, otherwise 0.</param>
    internal static SigilEntry Decide(ulong entrantCharId, GroupSnapshot group, IReadOnlyCollection<ulong> padOccupants,
                                      ulong liveOwnerCharId) {
        if (group is null) {
            return new SigilEntry(entrantCharId, ResetInstance: true);
        }

        if (group.GetMember(liveOwnerCharId)?.IsOnline == true) {
            return new SigilEntry(liveOwnerCharId, ResetInstance: false);
        }

        var owner = padOccupants.Contains(group.LeaderCharId) ? group.LeaderCharId : entrantCharId;

        return new SigilEntry(owner, ResetInstance: true);
    }

    /// <summary>
    /// The members told about a countdown, so those standing on the same pad start it together.
    /// </summary>
    internal static IReadOnlyList<GroupMemberSnapshot> CountdownRecipients(ulong entrantCharId, GroupSnapshot group)
        => [.. group.Members.Where(member => member.IsOnline && member.CharId != entrantCharId)];

}
