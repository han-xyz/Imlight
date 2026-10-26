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
 * GROUP SIGIL ENTRIES
 * ========================================================================
 * 
 * PURPOSE:
 * Holds, per group and dungeon zone, the members counting down on the sigil pad and the owner of the running
 * instance, so the members' sessions agree on one instance.
 * 
 * USAGE EXAMPLE:
 * GroupSigilEntries.Wait(group.GroupId, zone, charId);
 * var entry = GroupSigilEntries.Enter(charId, group, zone);
 * 
 * NOTE:
 * - In memory only. The owner's session releases the instance when the owner travels elsewhere.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Generic;
using System.Threading;
using Imlight.CoreLib.Game.Groups;

namespace Imlight.CoreLib.Game.World;

internal static class GroupSigilEntries {

    private static readonly Lock s_lock = new();
    private static readonly Dictionary<(ulong GroupId, string ZoneName), HashSet<ulong>> s_waiting = [];
    private static readonly Dictionary<(ulong GroupId, string ZoneName), ulong> s_owners = [];

    /// <summary>
    /// Records a member counting down on the pad that leads to <paramref name="zoneName"/>.
    /// </summary>
    internal static void Wait(ulong groupId, string zoneName, ulong charId) {
        lock (s_lock) {
            if (!s_waiting.TryGetValue((groupId, zoneName), out var waiting)) {
                waiting = [];
                s_waiting[(groupId, zoneName)] = waiting;
            }

            waiting.Add(charId);
        }
    }

    /// <summary>
    /// Removes a member whose countdown was cancelled.
    /// </summary>
    internal static void StopWaiting(ulong groupId, string zoneName, ulong charId) {
        lock (s_lock) {
            RemoveWaiting(groupId, zoneName, charId);
        }
    }

    /// <summary>
    /// Decides the instance a member enters when their countdown ends, and records its owner for the members after them.
    /// </summary>
    internal static SigilEntry Enter(ulong charId, GroupSnapshot group, string zoneName) {
        lock (s_lock) {
            var key = (group.GroupId, zoneName);
            s_owners.TryGetValue(key, out var liveOwnerCharId);
            IReadOnlyCollection<ulong> occupants = s_waiting.TryGetValue(key, out var waiting) ? waiting : [];

            var entry = SigilEntryPlan.Decide(charId, group, occupants, liveOwnerCharId);
            s_owners[key] = entry.OwnerCharId;
            RemoveWaiting(group.GroupId, zoneName, charId);

            return entry;
        }
    }

    /// <summary>
    /// Ends the group's running instance of <paramref name="zoneName"/> when <paramref name="ownerCharId"/> still owns it,
    /// so the next member to enter starts a fresh run.
    /// </summary>
    internal static void Release(ulong groupId, string zoneName, ulong ownerCharId) {
        lock (s_lock) {
            if (s_owners.TryGetValue((groupId, zoneName), out var owner) && owner == ownerCharId) {
                s_owners.Remove((groupId, zoneName));
            }
        }
    }

    private static void RemoveWaiting(ulong groupId, string zoneName, ulong charId) {
        if (s_waiting.TryGetValue((groupId, zoneName), out var waiting) && waiting.Remove(charId) && waiting.Count == 0) {
            s_waiting.Remove((groupId, zoneName));
        }
    }

}
