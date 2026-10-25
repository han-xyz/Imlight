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
 * CUSTOM EMOTE OWNERSHIP
 * ========================================================================
 * 
 * PURPOSE:
 * Whether a player owns a custom emote, and which radial menu layouts a
 * player may save, so emotes sold in the crown shop are only played once bought.
 * 
 * USAGE EXAMPLE:
 * EmoteService checks played animations and saved radial menus through these.
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
using System.Linq;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.CrownShop;

internal static class CustomEmoteOwnership {

    private const int BitsPerWord = 32;

    public static bool Owns(CustomEmoteBehaviorTemplate emote, uint purchased1, uint purchased2, uint purchased3) {
        if (emote.m_isDefaultEmote) {
            return true;
        }

        var bit = emote.m_bitFieldNumber;
        var word = (bit / BitsPerWord) switch {
            0 => purchased1,
            1 => purchased2,
            2 => purchased3,
            _ => 0u,
        };

        return bit >= 0 && (word & (1u << (bit % BitsPerWord))) != 0;
    }

    /// <summary>
    /// Accepts a client radial menu that is exactly one encoded layout of at most <paramref name="maxSlots"/>
    /// distinct slots, each emote passing <paramref name="mayPlaceEmote"/>. Store <paramref name="canonical"/>, never the raw blob.
    /// </summary>
    public static bool TryAcceptRadialMenu(ByteString raw, int maxSlots, Func<uint, bool> mayPlaceEmote, out ByteString canonical) {
        canonical = default;
        if (!RadialMenuData.TryFromByteString(raw, out var menu)) {
            return false;
        }

        var encoded = menu.ToByteString();
        var hasTrailingBytes = !raw.IsEmpty && encoded.Length != raw.Length;
        if (hasTrailingBytes
            || menu.Slots.Count > maxSlots
            || menu.Slots.Select(s => s.m_slot).Distinct().Count() != menu.Slots.Count
            || !menu.Slots.All(s => s.m_theEmoteTemplateID == 0 || mayPlaceEmote(s.m_theEmoteTemplateID))) {
            return false;
        }

        canonical = encoded;
        return true;
    }

}
