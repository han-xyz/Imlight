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
 * ITEM LOCKS
 * ========================================================================
 * 
 * PURPOSE:
 * Keeps a wizard's locked item ids and decides which items selling and discarding may take.
 * 
 * USAGE EXAMPLE:
 * if (!ItemLocks.AllowsRemoval(wizard.LockedItemIds, itemId)) { return; }
 * 
 * NOTE:
 * - The client's own lock bit in m_itemFlags is not known, so the lock is kept server-side only.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Inventory;

internal static class ItemLocks {

    internal const int MaxLockedItems = 512;

    /// <summary>
    /// Locks or unlocks an item in the list.
    /// </summary>
    /// <returns>False when a new lock is refused because the list already holds MaxLockedItems ids.</returns>
    internal static bool TrySet(List<ulong> lockedIds, ulong itemId, bool isLocked) {
        if (!isLocked) {
            lockedIds.Remove(itemId);

            return true;
        }

        if (lockedIds.Contains(itemId)) {
            return true;
        }

        if (lockedIds.Count >= MaxLockedItems) {
            return false;
        }

        lockedIds.Add(itemId);

        return true;
    }

    internal static bool AllowsRemoval(IReadOnlyCollection<ulong> lockedIds, ulong itemId)
        => lockedIds?.Contains(itemId) != true;

    /// <summary>
    /// Picks the items a discard by template removes: unlocked items of that template, in inventory order, at most quantity of them.
    /// </summary>
    internal static List<ulong> PickDiscards(IEnumerable<(ulong ItemId, ulong TemplateId)> items, ulong templateId, int quantity,
        IReadOnlyCollection<ulong> lockedIds)
        => items.Where(item => item.TemplateId == templateId && AllowsRemoval(lockedIds, item.ItemId))
            .Select(item => item.ItemId)
            .Take(int.Max(quantity, 0))
            .ToList();

}
