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
 */

using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Game.Inventory;
using Xunit;

namespace Imlight.Tests;

public sealed class ItemLocksTests {

    [Fact]
    public void TrySet_LockedItemIsRefusedForRemovalUntilUnlocked() {
        var lockedIds = new List<ulong>();

        Assert.True(ItemLocks.TrySet(lockedIds, 7, isLocked: true));
        Assert.False(ItemLocks.AllowsRemoval(lockedIds, 7));
        Assert.True(ItemLocks.AllowsRemoval(lockedIds, 8));

        Assert.True(ItemLocks.TrySet(lockedIds, 7, isLocked: false));
        Assert.True(ItemLocks.AllowsRemoval(lockedIds, 7));
    }

    [Fact]
    public void TrySet_LockingTwiceKeepsOneEntry() {
        var lockedIds = new List<ulong>();

        ItemLocks.TrySet(lockedIds, 7, isLocked: true);
        ItemLocks.TrySet(lockedIds, 7, isLocked: true);

        Assert.Single(lockedIds);
    }

    [Fact]
    public void TrySet_RefusesANewLockOnceTheListIsFull() {
        var lockedIds = Enumerable.Range(1, ItemLocks.MaxLockedItems).Select(id => (ulong) id).ToList();

        Assert.False(ItemLocks.TrySet(lockedIds, 0, isLocked: true));
        Assert.True(ItemLocks.AllowsRemoval(lockedIds, 0));
        Assert.True(ItemLocks.TrySet(lockedIds, 1, isLocked: true));
    }

    [Fact]
    public void PickDiscards_TakesOnlyUnlockedItemsOfTheTemplateUpToTheQuantity() {
        var items = new List<(ulong ItemId, ulong TemplateId)> { (1, 50), (2, 60), (3, 50), (4, 50), (5, 50) };
        var lockedIds = new List<ulong> { 3 };

        Assert.Equal([1UL, 4UL], ItemLocks.PickDiscards(items, 50, 2, lockedIds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void PickDiscards_NothingForANonPositiveQuantity(int quantity) {
        var items = new List<(ulong ItemId, ulong TemplateId)> { (1, 50) };

        Assert.Empty(ItemLocks.PickDiscards(items, 50, quantity, []));
    }

}
