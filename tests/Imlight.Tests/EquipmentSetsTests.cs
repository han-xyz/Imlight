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
using System.Text;
using Imlight.CoreLib.Game.Inventory;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class EquipmentSetsTests {

    private static EquipmentSetRecord Set(uint name, params ulong[] itemIds) => new() {
        Name = name,
        Items = itemIds.Select(id => new EquipmentSetItem { ItemId = id, SlotName = "Hat" }).ToList()
    };

    [Fact]
    public void MissingItems_ListsEveryItemTheWizardNoLongerOwns() {
        var owned = new HashSet<ulong> { 1, 3 };

        Assert.Equal([2UL, 4UL], EquipmentSets.MissingItems(Set(9, 1, 2, 3, 4), owned));
        Assert.Empty(EquipmentSets.MissingItems(Set(9, 1, 3), owned));
    }

    [Fact]
    public void TrySave_ReplacesASetOfTheSameName() {
        var sets = new List<EquipmentSetRecord> { Set(9, 1) };

        Assert.True(EquipmentSets.TrySave(sets, Set(9, 2)));

        Assert.Equal(2UL, Assert.Single(sets).Items.Single().ItemId);
    }

    [Fact]
    public void TrySave_RefusesANewSetOnceFull() {
        var sets = Enumerable.Range(0, EquipmentSets.MaxSets).Select(name => Set((uint) name, 1)).ToList();

        Assert.False(EquipmentSets.TrySave(sets, Set(EquipmentSets.MaxSets, 1)));
        Assert.True(EquipmentSets.TrySave(sets, Set(0, 2)));
        Assert.Equal(EquipmentSets.MaxSets, sets.Count);
    }

    [Fact]
    public void TrySave_RefusesASetWithTooManyItems() {
        var itemIds = Enumerable.Range(1, EquipmentSets.MaxItemsPerSet + 1).Select(id => (ulong) id).ToArray();

        Assert.False(EquipmentSets.TrySave([], Set(9, itemIds)));
    }

    [Theory]
    [InlineData("12", true, 12u)]
    [InlineData("abc", false, 0u)]
    [InlineData("", false, 0u)]
    public void TryParseName_ReadsADecimalSetName(string text, bool parsed, uint expected) {
        Assert.Equal(parsed, EquipmentSets.TryParseName(Encoding.ASCII.GetBytes(text), out var name));
        Assert.Equal(expected, name);
    }

    [Fact]
    public void HexPreview_ShowsAtMostThirtyTwoBytes() {
        var bytes = Enumerable.Range(0, 40).Select(value => (byte) value).ToArray();

        Assert.Equal(64, EquipmentSets.HexPreview(bytes).Length);
        Assert.Equal("0001FF", EquipmentSets.HexPreview([0, 1, 255]));
    }

}
