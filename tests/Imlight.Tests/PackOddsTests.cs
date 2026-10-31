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
 * PACK ODDS TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the crown shop's pack Preview listing: per-item odds summed over slots, the tier fallback,
 * NoneChance, and the empty listing for anything that is not a sold pack.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.WizardData.Models.World;
using Xunit;

namespace Imlight.Tests;

public sealed class PackOddsTests {

    private const ulong PackId = 700;
    private const uint CardId = 9001;
    private const uint GearId = 9002;
    private const uint SnackId = 9003;

    private static readonly PackTierChances s_evenGates = new(Wildcard: 0.5, Rare: 0.5, Uncommon: 0.5, Common: 1.0);

    private static CoreTemplate? ItemTemplate(ulong id) => id == CardId ? new SpellTemplate() : new WizItemTemplate();

    private static DropTable Table(string name, double rollChance = 1.0, double noneChance = 0.0, params DropItem[] items)
        => new() { Name = name, RollChance = rollChance, NoneChance = noneChance, Items = [.. items] };

    private static DropItem Item(uint id, int weight = 1) => new() { ItemId = id.ToString(), Weight = weight };

    private static LootInfoList List(params DropTable[] tables) {
        var byName = tables.DistinctBy(table => table.Name).ToDictionary(table => table.Name);
        var pack = new BoosterPackTemplate { m_lootTables = [.. tables.Select(table => table.Name)] };

        return PackManager.ListPackOdds(pack, s_evenGates, name => byName.GetValueOrDefault(name), ItemTemplate);
    }

    private static LootRarity Rarity(LootInfoList listing, uint itemId)
        => listing.m_lootRarityList.m_loot.Single(rarity => (ulong) rarity.m_lootGid == itemId);

    [Fact]
    public void ListPackOdds_ListsAnItemInTwoSlotsOnceWithSummedOdds() {
        var listing = List(Table("Set-A", rollChance: 0.25, items: Item(GearId)), Table("Set-B", rollChance: 0.5, items: Item(GearId)));

        Assert.Single(listing.m_loot);
        var rarity = Assert.Single(listing.m_lootRarityList.m_loot);
        Assert.Equal(0.75f, rarity.m_expectedChance);
        Assert.Equal(75, rarity.m_odds);
        Assert.Equal(0f, rarity.m_expectedValue);
    }

    [Fact]
    public void ListPackOdds_FollowsTheTierFallback() {
        var listing = List(Table("Set-Rare", items: Item(CardId)), Table("Set-Uncommon", items: Item(GearId)), Table("Set-Common", items: Item(SnackId)));

        Assert.Equal(0.5f, Rarity(listing, CardId).m_expectedChance);
        Assert.Equal(0.75f, Rarity(listing, GearId).m_expectedChance);
    }

    [Fact]
    public void ListPackOdds_CapsTheChanceAtCertain() {
        var listing = List(Table("Set-Common", items: Item(GearId)), Table("Set-Common", items: Item(GearId)));

        Assert.Equal(1f, Rarity(listing, GearId).m_expectedChance);
        Assert.Equal(100, Rarity(listing, GearId).m_odds);
    }

    [Fact]
    public void ListPackOdds_SplitsATableByItemWeight() {
        var listing = List(Table("Set-A", items: [Item(CardId), Item(GearId, weight: 3)]));

        Assert.Equal(25, Rarity(listing, CardId).m_odds);
        Assert.Equal(75, Rarity(listing, GearId).m_odds);
    }

    [Fact]
    public void ListPackOdds_NoneChanceLowersTheOdds() {
        var listing = List(Table("Set-Rare", noneChance: 0.25, items: Item(CardId)), Table("Set-Common", items: Item(GearId)));

        Assert.Equal(0.75f, Rarity(listing, CardId).m_expectedChance);
    }

    [Fact]
    public void ListPackOdds_NoneChanceLeavesAnUngatedTableAlone() {
        var listing = List(Table("Set-A", noneChance: 0.5, items: Item(GearId)));

        Assert.Equal(1f, Rarity(listing, GearId).m_expectedChance);
    }

    [Fact]
    public void ListPackOdds_ATableWithNullItemsGivesNothing() {
        var listing = List(new DropTable { Name = "Set-A", Items = null! }, Table("Set-B", items: Item(GearId)));

        Assert.Equal(100, Assert.Single(listing.m_lootRarityList.m_loot).m_odds);
    }

    [Fact]
    public void ListPackOdds_APossibleItemNeverShowsZeroPercent() {
        var listing = List(Table("Set-A", items: [Item(GearId), Item(CardId, weight: 999)]));

        Assert.Equal(1, Rarity(listing, GearId).m_odds);
    }

    [Fact]
    public void ListPackOdds_LeavesOutAnItemThatCannotDrop() {
        var listing = List(Table("Set-A", items: [Item(GearId, weight: 0), Item(CardId)]));

        Assert.Equal([(ulong) CardId], listing.m_lootRarityList.m_loot.Select(rarity => (ulong) rarity.m_lootGid));
    }

    [Fact]
    public void ListPackOdds_BuildsTheRollPathsLootInfoAndRarity() {
        var listing = List(Table("Set-Rare", items: Item(CardId)));

        var card = Assert.IsType<TreasureCardLootInfo>(Assert.Single(listing.m_loot));
        Assert.Equal(CardId, card.m_spellID);
        Assert.Equal(RarityType.RT_RARE, Rarity(listing, CardId).m_rarity);
    }

    [Fact]
    public void ListPackOdds_IsEmptyWhenNoTableExists() {
        var pack = new BoosterPackTemplate { m_lootTables = ["Gone"] };

        var listing = PackManager.ListPackOdds(pack, s_evenGates, _ => null, ItemTemplate);

        Assert.Empty(listing.m_loot);
        Assert.Empty(listing.m_lootRarityList.m_loot);
    }

    [Fact]
    public void SerializePackOdds_ASoldPackSerializesItsListing() {
        var table = Table("Set-A", items: Item(GearId));

        var data = PackManager.SerializePackOdds(PackId, _ => true, s_evenGates, _ => table, TemplateWithPack);

        var listing = Deserialize(data);
        Assert.Single(listing.m_loot);
        Assert.Equal(100, Assert.Single(listing.m_lootRarityList.m_loot).m_odds);
    }

    [Fact]
    public void SerializePackOdds_AnUnsoldPackIsEmpty() {
        var table = Table("Set-A", items: Item(GearId));

        var data = PackManager.SerializePackOdds(PackId, _ => false, s_evenGates, _ => table, TemplateWithPack);

        Assert.Empty(Deserialize(data).m_loot);
    }

    [Fact]
    public void SerializePackOdds_ASoldNonPackIsEmpty() {
        var table = Table("Set-A", items: Item(GearId));

        var data = PackManager.SerializePackOdds(GearId, _ => true, s_evenGates, _ => table, TemplateWithPack);

        Assert.Empty(Deserialize(data).m_loot);
    }

    [Fact]
    public void SerializePackOdds_AnUnknownTemplateIsEmpty() {
        var data = PackManager.SerializePackOdds(PackId, _ => true, s_evenGates, _ => null, _ => null);

        Assert.Empty(Deserialize(data).m_loot);
    }

    private static CoreTemplate? TemplateWithPack(ulong id)
        => id == PackId ? new BoosterPackTemplate { m_lootTables = ["Set-A"] } : ItemTemplate(id);

    private static LootInfoList Deserialize(Imcodec.IO.ByteString data) {
        var serializer = new ObjectSerializer(Versionable: false);
        Assert.True(serializer.Deserialize<LootInfoList>((byte[]) data, (uint) PropertyFlags.Prop_Public, out var listing));

        return listing;
    }

}
