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
 * EQUIPMENT SETS
 * ========================================================================
 * 
 * PURPOSE:
 * Rules for a wizard's saved equipment sets: how many are kept, and whether a set can be applied.
 * 
 * USAGE EXAMPLE:
 * if (EquipmentSets.MissingItems(set, ownedIds).Count > 0) { return; }
 * 
 * NOTE:
 * - The wire format of the client's Set and m_serializedItems strings is not known yet; HexPreview logs it.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Inventory;

internal static class EquipmentSets {

    internal const int MaxSets = 32;
    internal const int MaxItemsPerSet = 32;
    private const int HexPreviewBytes = 32;

    /// <summary>
    /// Stores a set, replacing the one of the same name.
    /// </summary>
    /// <returns>False when the set has more than MaxItemsPerSet items, or is new and MaxSets are already kept.</returns>
    internal static bool TrySave(List<EquipmentSetRecord> sets, EquipmentSetRecord set) {
        if (set.Items.Count > MaxItemsPerSet) {
            return false;
        }

        var index = sets.FindIndex(existing => existing.Name == set.Name);
        if (index >= 0) {
            sets[index] = set;

            return true;
        }

        if (sets.Count >= MaxSets) {
            return false;
        }

        sets.Add(set);

        return true;
    }

    /// <summary>
    /// Lists the set's items the wizard no longer owns; a set applies only when this is empty.
    /// </summary>
    internal static List<ulong> MissingItems(EquipmentSetRecord set, IReadOnlySet<ulong> ownedIds)
        => set.Items.Select(item => item.ItemId).Where(itemId => !ownedIds.Contains(itemId)).ToList();

    internal static bool TryParseName(byte[] set, out uint name)
        => uint.TryParse(Encoding.ASCII.GetString(set), NumberStyles.None, CultureInfo.InvariantCulture, out name);

    internal static string HexPreview(byte[] bytes)
        => Convert.ToHexString(bytes, 0, int.Min(bytes.Length, HexPreviewBytes));

}
