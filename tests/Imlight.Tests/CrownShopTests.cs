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
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.IO;
using System.Linq;
using Imlight.CoreLib.Game.CrownShop;
using Xunit;

namespace Imlight.Tests;

public sealed class CrownShopTests : IDisposable {

    private const ulong GoldItemId = 1001;
    private const ulong CrownsOnlyItemId = 1002;

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

    private static readonly string TwoItems = $$"""
        [
          { "templateId": {{GoldItemId}}, "goldCost": 500, "crownsCost": 50, "displayPriority": [0] },
          { "templateId": {{CrownsOnlyItemId}}, "crownsCost": 250, "displayPriority": [3, 1] }
        ]
        """;

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

}
