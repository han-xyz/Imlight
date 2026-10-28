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
 * ITEM LOOT INFO TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for the loot entry an item reward is sent as: reagents and pet snacks have their own loot types.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.DropTables;
using Xunit;

namespace Imlight.Tests;

public sealed class ItemLootInfoTests {

    [Fact]
    public void AReagent_IsSentAsReagentLoot() {
        var loot = DropTableConverter.ToItemLootInfo(100, 3, new ReagentItemTemplate());

        Assert.IsType<ReagentLootInfo>(loot);
        Assert.Equal(LOOT_TYPE.LOOT_TYPE_REAGENT, loot.m_lootType);
        Assert.Equal(3, loot.m_numItems);
    }

    [Fact]
    public void APetSnack_IsSentAsPetSnackLoot() {
        var loot = DropTableConverter.ToItemLootInfo(101, 1, new PetSnackItemTemplate());

        Assert.IsType<PetSnackLootInfo>(loot);
        Assert.Equal(LOOT_TYPE.LOOT_TYPE_PETSNACK, loot.m_lootType);
    }

    [Fact]
    public void Equipment_IsSentAsItemLoot() {
        var loot = DropTableConverter.ToItemLootInfo(102, 1, new WizItemTemplate());

        Assert.IsType<ItemLootInfo>(loot);
        Assert.Equal(LOOT_TYPE.LOOT_TYPE_ITEM, loot.m_lootType);
        Assert.Equal(102UL, loot.m_itemID.Full);
    }

    [Fact]
    public void AnUnknownTemplate_IsSentAsItemLoot() {
        Assert.Equal(LOOT_TYPE.LOOT_TYPE_ITEM, DropTableConverter.ToItemLootInfo(103, 1, null).m_lootType);
    }

}
