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
 * PACK SLOT ODDS
 * ========================================================================
 * 
 * PURPOSE:
 * Decides which loot table a crown pack slot rolls: each tiered slot passes its tier gate or falls down the chain
 * Wildcard, Rare, Uncommon, Common, so every opening grants its full slot count.
 * 
 * USAGE EXAMPLE:
 * var table = PackSlotOdds.ResolveSlot(slotTable, packTables, PackTierChances.FromConfiguration(), random, pity);
 * 
 * NOTE:
 * The tier comes from the slot's table name. A table whose RollChance or NoneChance differs from 1 and 0 uses its
 * own chance instead of the [Packs] one. A slot falls back only within its own hoard, the tables whose names share
 * its prefix (combo packs list two hoards). Pity counts live in memory and reset on restart.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Packs;

internal enum PackTier {
    Common,
    Uncommon,
    Rare,
    Wildcard
}

/// <summary>
/// The chance that a slot of each tier keeps its tier, read from the <c>[Packs]</c> section.
/// </summary>
internal sealed record PackTierChances(double Wildcard, double Rare, double Uncommon, double Common) {

    internal static PackTierChances Default { get; } = new(0.02, 0.06, 0.30, 1.0);

    /// <summary>
    /// The <c>Packs.Wildcard</c>, <c>Packs.Rare</c>, <c>Packs.Uncommon</c> and <c>Packs.Common</c> settings, each
    /// falling back to <see cref="Default"/> when unset.
    /// </summary>
    internal static PackTierChances FromConfiguration()
        => new(ConfigurationManager.GetValue("Packs.Wildcard", Default.Wildcard),
            ConfigurationManager.GetValue("Packs.Rare", Default.Rare),
            ConfigurationManager.GetValue("Packs.Uncommon", Default.Uncommon),
            ConfigurationManager.GetValue("Packs.Common", Default.Common));

    internal double For(PackTier tier) => tier switch {
        PackTier.Wildcard => Wildcard,
        PackTier.Rare => Rare,
        PackTier.Uncommon => Uncommon,
        _ => Common
    };

}

/// <summary>
/// The Rare and Wildcard gates one account has failed in a row on one pack type.
/// </summary>
internal sealed class PackPity {

    private readonly Lock _lock = new();
    private int _failedRareGates;

    internal bool IsDue(int threshold) {
        lock (_lock) {
            return threshold > 0 && _failedRareGates >= threshold;
        }
    }

    internal void Record(bool passed) {
        lock (_lock) {
            _failedRareGates = passed ? 0 : _failedRareGates + 1;
        }
    }

}

internal static class PackSlotOdds {

    private static readonly string[] s_tierWords = ["Wildcard", "Uncommon", "Rare", "Common"];

    /// <summary>
    /// The tier a pack slot table carries in its name, or <c>null</c> for a table that names none.
    /// </summary>
    internal static PackTier? TierOf(string tableName) {
        if (tableName.Contains("Wildcard", StringComparison.OrdinalIgnoreCase)) {
            return PackTier.Wildcard;
        }

        if (tableName.Contains("Uncommon", StringComparison.OrdinalIgnoreCase)) {
            return PackTier.Uncommon;
        }

        if (tableName.Contains("Rare", StringComparison.OrdinalIgnoreCase)) {
            return PackTier.Rare;
        }

        if (tableName.Contains("Common", StringComparison.OrdinalIgnoreCase)) {
            return PackTier.Common;
        }

        return null;
    }

    /// <summary>
    /// The table the slot at <paramref name="slotIndex"/> rolls this opening: the slot's own table when its tier gate
    /// passes, else a table of the next lower tier in the slot's hoard whose gate passes, taking the nearest one after
    /// the slot and wrapping around. The lowest tier the hoard carries needs no gate. A slot with no tier in its name
    /// keeps its own table, or yields <c>null</c> when it misses the table's own <see cref="DropTable.RollChance"/>.
    /// </summary>
    internal static DropTable? ResolveSlot(int slotIndex, IReadOnlyList<DropTable> packTables,
                                           PackTierChances chances, Random random, PackPity pity) {
        var slotTable = packTables[slotIndex];
        if (TierOf(slotTable.Name) is not { } slotTier) {
            return random.NextDouble() > slotTable.RollChance ? null : slotTable;
        }

        var hoard = HoardOf(slotTable.Name);
        var hoardTablesAfterSlot = Enumerable.Range(1, packTables.Count - 1)
            .Select(offset => packTables[(slotIndex + offset) % packTables.Count])
            .Where(table => HoardOf(table.Name).Equals(hoard, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var candidates = new List<(PackTier Tier, DropTable Table)> { (slotTier, slotTable) };
        for (var tier = slotTier - 1; tier >= PackTier.Common; tier--) {
            var tierTable = hoardTablesAfterSlot.FirstOrDefault(table => TierOf(table.Name) == tier);
            if (tierTable is not null) {
                candidates.Add((tier, tierTable));
            }
        }

        foreach (var (tier, table) in candidates.Take(candidates.Count - 1)) {
            if (PassesGate(tier, table, chances, random, pity)) {
                return table;
            }
        }

        return candidates[^1].Table;
    }

    private static string HoardOf(string tableName) {
        var tierStart = s_tierWords
            .Select(word => tableName.IndexOf(word, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(tableName.Length)
            .Min();

        return tierStart == 0 ? string.Empty : tableName[..(tableName.LastIndexOf('-', tierStart - 1) + 1)];
    }

    private static bool PassesGate(PackTier tier, DropTable table, PackTierChances chances, Random random, PackPity pity) {
        if (tier < PackTier.Rare) {
            return random.NextDouble() < GateChance(tier, table, chances);
        }

        var pityThreshold = (int) Math.Ceiling(table.PityCounter);
        var passes = (tier == PackTier.Rare && pity.IsDue(pityThreshold))
            || random.NextDouble() < GateChance(tier, table, chances);
        pity.Record(passes);

        return passes;
    }

    private static double GateChance(PackTier tier, DropTable table, PackTierChances chances) {
        var hasOwnChance = table.RollChance != 1.0 || table.NoneChance != 0.0;

        return hasOwnChance ? Math.Min(table.RollChance, 1.0 - table.NoneChance) : chances.For(tier);
    }

}
