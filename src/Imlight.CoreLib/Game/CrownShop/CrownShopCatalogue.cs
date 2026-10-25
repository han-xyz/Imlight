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
 * CROWN SHOP CATALOGUE
 * ========================================================================
 * 
 * PURPOSE:
 * The crown shop tabs, categories and items read from the data pack's
 * CrownShop/*.json files, checked for prices a purchase could abuse.
 * 
 * USAGE EXAMPLE:
 * CrownShopHandler.Load reads SpiralDBLocalPath/CrownShop through CrownShopCatalogue.Load.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.CrownShop;

internal sealed class CrownShopCatalogue {

    public static CrownShopCatalogue Empty { get; } = new([], []);

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // WishlistData.Encode: magic u32, version u32, keep flag u16, count u16, then per item template id u32 and u16 padding.
    private const int WishlistHeaderBytes = 12;
    private const int WishlistItemBytes = 6;

    private readonly Dictionary<ulong, CrownShopItem> _items;

    public IReadOnlyList<CrownShopTabConfig> Tabs { get; }

    public IReadOnlyCollection<CrownShopItem> Items => _items.Values;

    private CrownShopCatalogue(IReadOnlyList<CrownShopTabConfig> tabs, Dictionary<ulong, CrownShopItem> items) {
        Tabs = tabs;
        _items = items;
    }

    /// <summary>
    /// Reads every *.json tab file in <paramref name="directory"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">A tab file is not valid JSON, an item has a negative price, or two tabs price one item differently.</exception>
    public static CrownShopCatalogue Load(string directory) {
        var tabs = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(ReadTab)
            .OrderBy(t => t.TabId)
            .ToList();

        var items = new Dictionary<ulong, CrownShopItem>();
        foreach (var item in tabs.SelectMany(t => t.Items)) {
            if (items.TryGetValue(item.TemplateId, out var listed) && !HasSamePrices(listed, item)) {
                throw new InvalidDataException($"Crown shop item {item.TemplateId} is listed in two tabs with different prices.");
            }

            items[item.TemplateId] = ToCrownShopItem(item);
        }

        return new CrownShopCatalogue(tabs, items);
    }

    public bool TryGetItem(ulong templateId, out CrownShopItem item)
        => _items.TryGetValue(templateId, out item);

    /// <summary>
    /// Decodes a client wishlist that is exactly one encoded wishlist of at most <paramref name="maxSize"/>
    /// catalogue items. Store <c>wishlist.ToByteString()</c>, never <paramref name="raw"/>.
    /// </summary>
    public bool TryAcceptWishlist(ByteString raw, uint maxSize, [NotNullWhen(true)] out WishlistData wishlist) {
        wishlist = null;
        if (raw.Length > WishlistHeaderBytes + (WishlistItemBytes * maxSize)
            || !WishlistData.TryFromByteString(raw, out var decoded)) {
            return false;
        }

        var hasTrailingBytes = !raw.IsEmpty && raw.Length != WishlistHeaderBytes + (WishlistItemBytes * decoded.Items.Count);
        if (hasTrailingBytes || decoded.Items.Count > maxSize || !decoded.Items.All(id => _items.ContainsKey(id))) {
            return false;
        }

        wishlist = decoded;
        return true;
    }

    private static CrownShopTabConfig ReadTab(string path) {
        CrownShopTabConfig tab;
        try {
            tab = JsonSerializer.Deserialize<CrownShopTabConfig>(File.ReadAllText(path), s_jsonSerializerOptions);
        } catch (JsonException e) {
            throw new InvalidDataException($"Crown shop tab {path} is not valid JSON: {e.Message}", e);
        }

        if (tab is null) {
            throw new InvalidDataException($"Crown shop tab {path} is empty.");
        }

        var negativelyPriced = tab.Items.FirstOrDefault(i => i.GoldCost < 0 || i.CrownsCost < 0 || i.TicketCost < 0);
        if (negativelyPriced is not null) {
            throw new InvalidDataException($"Crown shop tab {path} gives item {negativelyPriced.TemplateId} a negative price.");
        }

        return tab;
    }

    private static bool HasSamePrices(CrownShopItem listed, CrownShopItemConfig item)
        => listed.m_goldCost == item.GoldCost && listed.m_crownsCost == item.CrownsCost && listed.m_ticketCost == item.TicketCost;

    private static CrownShopItem ToCrownShopItem(CrownShopItemConfig item) => new() {
        m_itemTemplateId = item.TemplateId,
        m_itemFlags = item.ItemFlags,
        m_goldCost = item.GoldCost,
        m_strikethruGold = item.StrikethruGold,
        m_crownsCost = item.CrownsCost,
        m_strikethruCrowns = item.StrikethruCrowns,
        m_ticketCost = item.TicketCost,
        m_displayPriority = string.Join(',', item.DisplayPriority.Select((priority, index) => $"{priority}:{index}")),
        m_combatOnly = item.CrownShopItemFlags.CombatOnly,
        m_noGift = item.CrownShopItemFlags.NoGift,
        m_recommendIfOwned = item.CrownShopItemFlags.RecommendIfOwned,
        m_saleID = 0,
        m_description = "",
        m_segReqsPoolsStatements = [],
        m_segReqsStatement = "",
        // Example: HasBadge(Raid01_QuestComplete_01) = True
        // Example: ExcludeIfNoSegData = True,HasBadge(FinishAR-PostLM-MAIN-001) = True
        // Example: ExcludeIfNoSegData = True,NLevel &gt; 169,HasBadge(WinNightmare) = True,HasBadge(FinishAR-PostWL-MAIN-001) = True
    };

}
