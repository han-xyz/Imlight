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
 * Chooses the owner of a group's dungeon run and the members who hear a sigil countdown.
 * 
 * USAGE EXAMPLE:
 * var ownerCharId = SigilEntryPlan.ChooseOwner(charId, group, padOccupants);
 * 
 * NOTE:
 * - The group leader owns the run when online and on the pad, otherwise the first member to enter. DungeonRunBook
 *   keeps the run.
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
/// The instance a dungeon entry or a teleport inside a dungeon run is sent to.
/// </summary>
/// <param name="OwnerCharId">The owner of the instance container the entrant joins.</param>
/// <param name="ResetInstance">Whether the transfer drops the loaded copy of the destination and builds a fresh one.</param>
internal sealed record InstanceEntry(ulong OwnerCharId, bool ResetInstance);

internal static class SigilEntryPlan {

    /// <summary>
    /// Chooses the owner of a group's new dungeon run: the group leader when they are online and counting down on the
    /// pad, otherwise the wizard whose countdown ended first.
    /// </summary>
    /// <param name="entrantCharId">The wizard whose countdown ended.</param>
    /// <param name="group">The entrant's group.</param>
    /// <param name="padOccupants">The wizards counting down on the pad, the entrant included.</param>
    internal static ulong ChooseOwner(ulong entrantCharId, GroupSnapshot group, IReadOnlyCollection<ulong> padOccupants)
        => padOccupants.Contains(group.LeaderCharId) && group.GetMember(group.LeaderCharId)?.IsOnline == true
            ? group.LeaderCharId
            : entrantCharId;

    /// <summary>
    /// The members told about a countdown, so those standing on the same pad start it together.
    /// </summary>
    internal static IReadOnlyList<GroupMemberSnapshot> CountdownRecipients(ulong entrantCharId, GroupSnapshot group)
        => [.. group.Members.Where(member => member.IsOnline && member.CharId != entrantCharId)];

}
