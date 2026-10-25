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
 * COMBAT SPELL DECK MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages the Crown Shop configuration and runtime data. Loads Crown Shop tabs,
 * categories, and items from JSON configuration files, converts them into the
 * corresponding game objects, and serializes the complete Crown Shop data structure for client consumption.
 * 
 * USAGE EXAMPLE:
 * 
 * 
 * NOTE:
 * 
 * Created by: Phill030
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Imlight.CoreLib.Game.CrownShop;

public static class CrownShopHandler {
    private static readonly bool s_enabled
        = ConfigurationManager.Settings["CrownShop.Enabled"].AsBool();
    private static readonly uint s_wishlistMaxSize
        = ConfigurationManager.Settings["CrownShop.WishlistMaxSize"].AsUInt();
    private static readonly uint s_wishlistSBExpansionSize
    = ConfigurationManager.Settings["CrownShop.WishlistSBExpansionSize"].AsUInt();
    private static readonly bool s_autoReloadOnChanges
        = ConfigurationManager.Settings["CrownShop.AutoReloadOnChanges"].AsBool();
    public static readonly uint s_maxBuyCount
        = ConfigurationManager.Settings["CrownShop.MaxBuyCount"].AsUInt();
    private static readonly string s_crownShopConfigPath
        = ConfigurationManager.Settings["Database.SpiralDBLocalPath"] + "/CrownShop";

    private static FileSystemWatcher s_watcher;
    private static DateTime s_lastReloadTime = DateTime.MinValue;
    private static readonly object s_reloadLock = new();

    private sealed record Snapshot(CrownShopCatalogue Catalogue, ByteString SerializedData);

    private static volatile Snapshot s_snapshot = new(CrownShopCatalogue.Empty, new());

    /// <exception cref="InvalidDataException">A tab file in the data pack is malformed.</exception>
    public static void Load() {
        if (!s_enabled) {
            Logger.Information("CrownShop not initializing (disabled in config)");
            return;
        }

        var basePath = Path.GetFullPath(s_crownShopConfigPath);
        if (!Directory.Exists(basePath)) {
            Logger.Warning("CrownShop configuration directory not found: {0}", Logger.Args(basePath));
            return;
        }

        LoadCatalogue(basePath);

        if (s_autoReloadOnChanges && s_watcher == null) {
            SetupAutoReloadWatcher(basePath);
        }
    }

    private static void LoadCatalogue(string basePath) {
        lock(s_reloadLock) {
            Logger.Information("Loading CrownShop configuration from: {0}", Logger.Args(s_crownShopConfigPath));

            var catalogue = CrownShopCatalogue.Load(basePath);

            var layoutTabs = catalogue.Tabs.Select(tab => new CrownShopCategoryMenu() {
                m_ID = tab.TabId,
                m_name = tab.Name,
                m_iconResource = tab.Icon,
                m_categoryIDs = tab.CategoryIds,
                m_tags = tab.Tags,
                m_description = tab.Description
            }).ToList();

            var layoutCategories = catalogue.Tabs.SelectMany(t => t.Categories).Select(category => new CrownShopCategory() {
                m_ID = category.CategoryId,
                m_parentTabID = category.ParentTabId,
                m_name = category.Name,
                m_description = category.Description,
                m_iconResource = category.Icon,
                m_tags = category.Tags,
                m_allowMultipleBuy = category.Flags.AllowMultipleBuy,
                m_forceDisallowMultipleBuy = category.Flags.ForceDisallowMultipleBuy,
                m_dontFilterOwnedRecoItems = category.Flags.DontFilterOwnedRecoItems,
                m_isHousesCategory = category.Flags.IsHousesCategory,
                m_isEverythingCategory = category.Flags.IsEverythingCategory,
                m_isGroupElixirsCategory = category.Flags.IsGroupElixirsCategory
            }).ToList();

            var crownShopLayout = new CrownShopLayout() {
                m_tabs = layoutTabs,
                m_categories = layoutCategories
            };

            var crownShopData = new CrownShopData() {
                m_items = catalogue.Items.ToList(),
                m_crownShopLayout = crownShopLayout,
                m_wishlistMaxSize = (int) s_wishlistMaxSize,
                m_wishlistSBExpansionSize = (int) s_wishlistSBExpansionSize,
                // Probably has something todo with MSG_PCS_SEGDATA_RESPONSE
                m_crownShopSegReqsSummary = new(),
                // todo: recommended items?
                m_recomendedItems = new LevelData()
            };

            var serializer = new ObjectSerializer(
                Versionable: false,
                Behaviors: SerializerFlags.SerializeFlags | SerializerFlags.Compress
            );

            var propertyFlags = PropertyFlags.Prop_Save | PropertyFlags.Prop_Public;
            if (!serializer.Serialize(crownShopData, propertyFlags, out var serializedData)) {
                Logger.Error("Failed to serialize CrownShopData");
                return;
            }

            s_snapshot = new Snapshot(catalogue, serializedData);

            Logger.Information("CrownShop successfully initialized: {0} tabs, {1} categories.",
                Logger.Args(layoutTabs.Count, layoutCategories.Count));
        }
    }

    private static void SetupAutoReloadWatcher(string basePath) {
        try {
            s_watcher = new FileSystemWatcher(basePath, "*.json") {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            s_watcher.Changed += OnConfigFileChanged;
            s_watcher.Created += OnConfigFileChanged;
            s_watcher.Renamed += (s, e) => OnConfigFileChanged(s, e);

            Logger.Information("CrownShop: Live hot-reload watcher enabled for {0}.", Logger.Args(s_crownShopConfigPath));
        } catch(Exception e) {
            Logger.Warning("Could not enable CrownShop auto-reload watcher: {0}", Logger.Args(e.Message));
        }
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e) {
        if ((DateTime.UtcNow - s_lastReloadTime).TotalMilliseconds < 500) {
            return;
        }

        s_lastReloadTime = DateTime.UtcNow;
        Logger.Information("CrownShop file changed. Reloading...");

        // An exception here would end the process from the watcher thread; the previous catalogue stays live.
        try {
            LoadCatalogue(Path.GetFullPath(s_crownShopConfigPath));
        } catch (Exception ex) when (ex is InvalidDataException or IOException) {
            Logger.Error("CrownShop reload failed, keeping the previous catalogue: {0}", Logger.Args(ex.Message));
        }
    }

    public static ByteString GetCrownShopData() => s_snapshot.SerializedData;

    public static List<CrownShopItem> GetCrownShopItems() => s_snapshot.Catalogue.Items.ToList();
    public static bool TryGetCrownShopItem(ulong id, out CrownShopItem item)
        => s_snapshot.Catalogue.TryGetItem(id, out item);

    public static bool IsAcceptableWishlist(ByteString wishlistData)
        => s_snapshot.Catalogue.IsAcceptableWishlist(wishlistData, s_wishlistMaxSize);

}
