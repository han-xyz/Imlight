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
 * Last Updated: 10/25/2026
 */

using System;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class CrownShopTests : IDisposable {

    private const ulong GoldItemId = 1001;
    private const ulong CrownsOnlyItemId = 1002;

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

    [Fact]
    public void BoosterPack_NeedsASlotPerLootTableAndPack() {
        var pack = new BoosterPackTemplate { m_lootTables = ["Common", "Rare", "Epic"] };

        Assert.Equal(6, GiftingService.CalculateRequiredInventorySlots(pack, 2));
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

    private void WriteTab(string fileName, int tabId, string itemsJson) {
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
                  "categoryId": {{tabId}},
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
