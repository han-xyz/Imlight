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
 * CROWN SHOP TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the crown shop catalogue loaded from the data pack,
 * purchase pricing and wishlist bounds.
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
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using Xunit;

namespace Imlight.Tests;

public sealed class CrownShopTests : IDisposable {

    private const ulong GoldItemId = 1001;
    private const ulong CrownsOnlyItemId = 1002;
    private const ulong HenchmanItemId = 1582001;

    private static readonly string? s_spiralDbClone = Environment.GetEnvironmentVariable("SPIRALDB_CLONE");

    private readonly string _directory = Directory.CreateTempSubdirectory("imlight-crownshop-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void CrownShop_LoadsAllTabs() {
        for (var tabId = 46; tabId >= 36; tabId--) {
            WriteTab($"Tab{tabId - 36}.json", tabId, tabId == 38 ? TwoItems : "[]");
        }
        File.WriteAllText(Path.Combine(_directory, "README.md"), "not a tab");

        var catalogue = CrownShopCatalogue.Load(_directory);

        Assert.Equal(Enumerable.Range(36, 11), catalogue.Tabs.Select(t => t.TabId));
        Assert.Equal(11, catalogue.Tabs.Sum(t => t.Categories.Count));
        Assert.True(catalogue.TryGetItem(CrownsOnlyItemId, out var item));
        Assert.Equal(250, item.m_crownsCost);
        Assert.Equal("3:0,1:1", item.m_displayPriority);
    }

    [CloneFact]
    public void CrownShop_LoadsRealDataPackTabs() {
        var catalogue = CrownShopCatalogue.Load(Path.Combine(s_spiralDbClone!, "CrownShop"));

        Assert.Equal(Enumerable.Range(36, 11), catalogue.Tabs.Select(t => t.TabId));
        Assert.True(catalogue.TryGetItem(1217734, out var item));
        Assert.Equal(3500, item.m_crownsCost);
        Assert.Equal(35000, item.m_goldCost);
    }

    [CloneFact]
    public void CrownShop_RealDataPackLayout_DefinesEveryReferencedCategoryOnce() {
        var catalogue = CrownShopCatalogue.Load(Path.Combine(s_spiralDbClone!, "CrownShop"));
        var categoryIds = catalogue.Categories.Select(c => c.CategoryId).ToList();
        var tabIds = catalogue.Tabs.Select(t => t.TabId).ToHashSet();

        Assert.Equal(categoryIds.Count, categoryIds.Distinct().Count());
        Assert.All(catalogue.Tabs.SelectMany(t => t.CategoryIds), id => Assert.Contains(id, categoryIds));
        Assert.All(catalogue.Tabs.SelectMany(t => t.Items).SelectMany(i => i.DisplayPriority),
            id => Assert.Contains((int) id, categoryIds));
        Assert.All(catalogue.Categories, c => Assert.Contains(c.ParentTabId, tabIds));
        Assert.Equal(categoryIds, catalogue.RecommendedItems.m_categoryData.Select(c => c.m_id));
    }

    [Fact]
    public void Load_CategoryDefinedInTwoTabs_IsListedOnceUnderTheFirstTab() {
        WriteTab("Tab2.json", 38, "[]", categoryId: 26);
        WriteTab("Tab4.json", 40, "[]", categoryId: 26);

        var catalogue = CrownShopCatalogue.Load(_directory);

        var category = Assert.Single(catalogue.Categories);
        Assert.Equal(26, category.CategoryId);
        Assert.Equal(38, category.ParentTabId);
    }

    [Fact]
    public void RecommendedItems_ListEveryCategoryWithItsItemsOnce() {
        WriteTab("Tab0.json", 36, "[]");
        WriteTab("Tab2.json", 38, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 10, "displayPriority": [38] }]""");
        WriteTab("Tab4.json", 40, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 10, "displayPriority": [38] }]""");

        var recommended = CrownShopCatalogue.Load(_directory).RecommendedItems.m_categoryData;

        Assert.Equal([36, 38, 40], recommended.Select(c => c.m_id));
        Assert.Empty(recommended[0].m_items);
        Assert.Equal([(int) GoldItemId], recommended[1].m_items.Select(i => i.m_templateID));
        Assert.Empty(recommended[2].m_items);
    }

    [Fact]
    public void SoldEmotes_SkipsItemWhoseTemplateThrows() {
        InitializeQuietLogging();
        WriteTab("Tab0.json", 36, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 10 }, { "templateId": {{CrownsOnlyItemId}}, "crownsCost": 250 }]""");
        var catalogue = CrownShopCatalogue.Load(_directory);
        var emote = new CustomEmoteBehaviorTemplate { m_emoteType = CustomEmoteType.CE_Emote };

        var emotes = CrownShopHandler.SoldEmotes.From(catalogue, id => id == GoldItemId
            ? throw new InvalidCastException()
            : new CoreTemplate { m_behaviors = [emote] });

        Assert.Equal([CrownsOnlyItemId], emotes.ByTemplateId.Keys);
    }

    private const uint CardId = 9001;
    private const uint GearId = 9002;

    private static CoreTemplate? PackItemTemplate(ulong id) => id == CardId ? new SpellTemplate() : new WizItemTemplate();

    private static DropTable PackTable(string name, uint itemId)
        => new() { Name = name, Items = [new DropItem { ItemId = itemId.ToString() }] };

    private static int WorstCase(IReadOnlyList<DropTable> tables, int count) {
        var byName = tables.DistinctBy(table => table.Name).ToDictionary(table => table.Name);
        var pack = new BoosterPackTemplate { m_lootTables = [.. tables.Select(table => table.Name)] };

        return PackManager.WorstCaseBackpackSlots(pack, count, name => byName.GetValueOrDefault(name), PackItemTemplate);
    }

    [Fact]
    public void BoosterPack_SlotsThatOnlyDropTreasureCardsNeedNoBackpackSlot() {
        var tables = new[] {
            PackTable("Set-Wildcard", GearId), PackTable("Set-Rare", CardId), PackTable("Set-Uncommon", CardId),
            PackTable("Set-Common", CardId), PackTable("Set-Common", CardId)
        };

        Assert.Equal(1, WorstCase(tables, 1));
    }

    [Fact]
    public void BoosterPack_EverySlotThatCanDropGearNeedsASlotPerOpening() {
        var tables = new[] {
            PackTable("Set-Wildcard", GearId), PackTable("Set-Rare", GearId), PackTable("Set-Common", GearId)
        };

        Assert.Equal(tables.Length * 2 * 3, WorstCase(tables, 6));
    }

    [Fact]
    public void BoosterPack_AFullBackpackIsRefusedFromTheTablesAlone_AndTwentyTwoOfSeventyFiveFit() {
        var inventory = new ServerWizInventoryBehavior { Items = [] };
        inventory.ExtraSpace = 75 - inventory.NumItemsAllowed;
        for (var i = 0; i < 53; i++) {
            inventory.Items.Add(new WizClientObjectItem());
        }

        var tables = Enumerable.Range(0, 22).Select(i => PackTable($"Set{i}-Common", GearId)).ToArray();

        Assert.True(inventory.HasInventorySpace(WorstCase(tables, 1)));
        Assert.False(inventory.HasInventorySpace(WorstCase(tables, 2)));
    }

    [Fact]
    public void PackLoot_TreasureCardsAndJewelsAreNotBackpackSlots_ButItemsAre() {
        var inventory = new ServerWizInventoryBehavior { Items = [] };
        for (var i = 0; i < inventory.NumItemsAllowed - 1; i++) {
            inventory.Items.Add(new WizClientObjectItem());
        }

        var wizard = new Wizard { InventoryBehavior = inventory };
        (uint, CoreTemplate) card = (1, new SpellTemplate());
        (uint, CoreTemplate) item = (2, new WizItemTemplate());

        Assert.True(PackManager.HasRoomFor(wizard, [card, card, item]));
        Assert.False(PackManager.HasRoomFor(wizard, [item, item]));
    }

    [Fact]
    public void PackLoot_EachPackFitsButBothOverflowTheBag_IsRefused() {
        const uint ReagentId = 7001;
        var reagent = new ReagentItemTemplate();
        var wizard = new Wizard {
            AlchemyBehavior = new ServerAlchemyBehavior {
                Reagents = [new ClientReagentItem { m_templateID = ReagentId, m_quantity = ServerAlchemyBehavior.MaxReagentStack - 1 }]
            }
        };
        (uint, CoreTemplate) drop = (ReagentId, reagent);

        Assert.True(PackManager.HasRoomFor(wizard, [drop]));
        Assert.False(PackManager.HasRoomFor(wizard, [drop, drop]));
    }

    [Fact]
    public void Load_SameItemWithDifferentPrices_Throws() {
        WriteTab("Tab0.json", 36, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 10 }]""");
        WriteTab("Tab1.json", 37, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 1 }]""");

        Assert.Throws<InvalidDataException>(() => CrownShopCatalogue.Load(_directory));
    }

    [Fact]
    public void Load_SameItemWithSamePrices_Loads() {
        WriteTab("Tab0.json", 36, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 10 }]""");
        WriteTab("Tab1.json", 37, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 10 }]""");

        Assert.True(CrownShopCatalogue.Load(_directory).TryGetItem(GoldItemId, out _));
    }

    [Fact]
    public void Load_ItemWithoutDisplayPriority_Loads() {
        WriteTab("Tab0.json", 36, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": 10, "displayPriority": [] }]""");

        var catalogue = CrownShopCatalogue.Load(_directory);

        Assert.True(catalogue.TryGetItem(GoldItemId, out var item));
        Assert.Equal("", item.m_displayPriority);
    }

    [Fact]
    public void Load_HenchmanCategoryItem_IsCombatOnlyHenchman() {
        WriteTab("Tab9.json", 45, $$"""[{ "templateId": {{HenchmanItemId}}, "crownsCost": 400, "displayPriority": [18] }]""");
        WriteTab("Tab0.json", 38, TwoItems);

        var catalogue = CrownShopCatalogue.Load(_directory);

        Assert.True(catalogue.TryGetItem(HenchmanItemId, out var henchman));
        Assert.True(henchman.m_combatOnly);
        Assert.True(henchman.m_noGift);
        Assert.True(catalogue.IsHenchman(HenchmanItemId));
        Assert.True(catalogue.TryGetItem(CrownsOnlyItemId, out var other));
        Assert.False(other.m_combatOnly);
        Assert.False(catalogue.IsHenchman(CrownsOnlyItemId));
    }

    [Fact]
    public void Load_MalformedTab_ThrowsNamingTheFile() {
        WriteTab("Tab0.json", 36, "[]");
        File.WriteAllText(Path.Combine(_directory, "Tab1.json"), "{ \"tabId\": ");

        var error = Assert.Throws<InvalidDataException>(() => CrownShopCatalogue.Load(_directory));

        Assert.Contains("Tab1.json", error.Message);
    }

    [Fact]
    public void Load_NegativeCost_Throws() {
        WriteTab("Tab0.json", 36, $$"""[{ "templateId": {{GoldItemId}}, "goldCost": -5 }]""");

        Assert.Throws<InvalidDataException>(() => CrownShopCatalogue.Load(_directory));
    }

    [Fact]
    public void TryCompute_MultipliesUnitPriceByCount() {
        var item = new CrownShopItem { m_goldCost = 40, m_crownsCost = 3 };

        Assert.True(CrownShopPrice.TryCompute(item, CrownShopPrice.GoldCurrency, 5, out var gold));
        Assert.True(CrownShopPrice.TryCompute(item, CrownShopPrice.CrownsCurrency, 5, out var crowns));
        Assert.Equal(200, gold);
        Assert.Equal(15, crowns);
    }

    [Fact]
    public void TryCompute_CurrencyWithoutPrice_IsRejected() {
        var crownsOnly = new CrownShopItem { m_goldCost = 0, m_crownsCost = 250 };

        Assert.False(CrownShopPrice.TryCompute(crownsOnly, CrownShopPrice.GoldCurrency, 1, out _));
    }

    [Fact]
    public void TryCompute_UnknownCurrency_IsRejected() {
        var item = new CrownShopItem { m_goldCost = 40, m_crownsCost = 3 };

        Assert.False(CrownShopPrice.TryCompute(item, 2, 1, out _));
    }

    [Fact]
    public void TryCompute_OverflowingTotal_IsRejected() {
        var item = new CrownShopItem { m_crownsCost = int.MaxValue / 2 };

        Assert.False(CrownShopPrice.TryCompute(item, CrownShopPrice.CrownsCurrency, 3, out _));
    }

    [Fact]
    public void TryAcceptWishlist_CatalogueItemsWithinSize_IsAccepted() {
        var catalogue = LoadTwoItems();
        var wishlist = new WishlistData { Items = [(uint) GoldItemId, (uint) CrownsOnlyItemId] }.ToByteString();

        Assert.True(catalogue.TryAcceptWishlist(wishlist, maxSize: 2, out _));
    }

    [Fact]
    public void TryAcceptWishlist_OverMaxSize_IsRejected() {
        var catalogue = LoadTwoItems();
        var wishlist = new WishlistData { Items = [(uint) GoldItemId, (uint) CrownsOnlyItemId] }.ToByteString();

        Assert.False(catalogue.TryAcceptWishlist(wishlist, maxSize: 1, out _));
    }

    [Fact]
    public void TryAcceptWishlist_ItemNotInCatalogue_IsRejected() {
        var catalogue = LoadTwoItems();
        var wishlist = new WishlistData { Items = [(uint) GoldItemId, 9999] }.ToByteString();

        Assert.False(catalogue.TryAcceptWishlist(wishlist, maxSize: 30, out _));
    }

    [Fact]
    public void TryAcceptWishlist_UndecodableData_IsRejected() {
        var catalogue = LoadTwoItems();

        Assert.False(catalogue.TryAcceptWishlist("not a wishlist", maxSize: 30, out _));
    }

    [Fact]
    public void Wishlist_TrailingBytes_Rejected() {
        var catalogue = LoadTwoItems();
        byte[] wishlist = new WishlistData { Items = [(uint) GoldItemId] }.ToByteString();

        Assert.False(catalogue.TryAcceptWishlist(wishlist.Concat(new byte[] { 0xAB, 0xCD }).ToArray(), maxSize: 30, out _));
    }

    [Fact]
    public void Wishlist_StoredCanonical() {
        var catalogue = LoadTwoItems();
        byte[] canonical = new WishlistData { Items = [(uint) GoldItemId, (uint) CrownsOnlyItemId] }.ToByteString();
        var nonCanonical = (byte[]) canonical.Clone();
        nonCanonical[8] = 5;
        nonCanonical[16] = 0x7F;

        Assert.True(catalogue.TryAcceptWishlist(nonCanonical, maxSize: 30, out var wishlist));
        Assert.Equal(canonical, (byte[]) wishlist.ToByteString());
    }

    [Fact]
    public void RemovedWishlistItems_ListsWhatTheNewWishlistDropped() {
        var before = new WishlistData { Items = [1, 2, 3] }.ToByteString();
        var after = new WishlistData { Items = [2] }.ToByteString();

        Assert.Equal("1,3", CrownShopWishlist.RemovedItems(before, after));
    }

    [Fact]
    public void RemovedWishlistItems_NothingStoredYet_IsEmpty() {
        var after = new WishlistData { Items = [2] }.ToByteString();

        Assert.Equal("", CrownShopWishlist.RemovedItems(default, after));
    }

    private static readonly string TwoItems = $$"""
        [
          { "templateId": {{GoldItemId}}, "goldCost": 500, "crownsCost": 50, "displayPriority": [0] },
          { "templateId": {{CrownsOnlyItemId}}, "crownsCost": 250, "displayPriority": [3, 1] }
        ]
        """;

    private CrownShopCatalogue LoadTwoItems() {
        WriteTab("Tab0.json", 38, TwoItems);
        return CrownShopCatalogue.Load(_directory);
    }

    private void WriteTab(string fileName, int tabId, string itemsJson, int? categoryId = null) {
        var json = $$"""
            {
              "tabId": {{tabId}},
              "name": "CrownShopSWF_Menu{{tabId}}",
              "icon": "GUI/CrownShopIcons/Categories/Sale_Items.dds",
              "categoryIds": [{{tabId}}],
              "tags": "Seperate",
              "description": "Tab {{tabId}}",
              "categories": [
                {
                  "categoryId": {{categoryId ?? tabId}},
                  "parentTabId": {{tabId}},
                  "name": "CrownShopSWF_Category{{tabId}}",
                  "icon": "GUI/CrownShopIcons/Categories/Sale_Items.dds",
                  "flags": { "allowMultipleBuy": true }
                }
              ],
              "items": {{itemsJson}}
            }
            """;
        File.WriteAllText(Path.Combine(_directory, fileName), json);
    }

    private sealed class CloneFactAttribute : FactAttribute {
        public CloneFactAttribute() {
            if (s_spiralDbClone is null || !Directory.Exists(s_spiralDbClone)) {
                Skip = "set SPIRALDB_CLONE to a spiraldb clone to run this test";
            }
        }
    }

    private static void InitializeQuietLogging() {
        var iniFilePath = Path.GetTempFileName();
        var logPath = Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt");
        try {
            File.WriteAllText(iniFilePath, $"[Logging]\nLogPath = {logPath}\nLogLevel = ERROR\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

}
