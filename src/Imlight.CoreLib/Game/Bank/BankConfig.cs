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
 * BANK CONFIG
 * ========================================================================
 *
 * PURPOSE:
 * Reads the bank settings of Imlight.ini: the default capacities of a character's bank and of an account's
 * shared bank, the shared jewel cap, the object names of the NPCs that open the bank and the two settings of
 * an expansion.
 *
 * USAGE EXAMPLE:
 * if (BankConfig.IsEnabled) { ... }
 *
 * NOTE:
 * An unset, empty or 0-or-less [Character] MaxBankItems or MaxSharedBankItems is the retail capacity of 100 and
 * logs a warning once, so a stray empty key never switches the bank off.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Threading;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Bank;

/// <summary>
/// Settings of the bank, read from the [Character] and [Bank] sections of Imlight.ini.
/// </summary>
internal static class BankConfig {

    private const string MAX_BANK_ITEMS_KEY = "Character.MaxBankItems";
    private const string MAX_SHARED_BANK_ITEMS_KEY = "Character.MaxSharedBankItems";
    private const string SHARED_JEWEL_CAP_KEY = "Bank.SharedJewelCap";
    private const string OBJECT_NAMES_KEY = "Bank.ObjectNames";
    private const string EXPAND_ELIXIR_AMOUNT_KEY = "Bank.ExpandElixirAmount";
    private const string MAX_BANK_LIMIT_KEY = "Bank.MaxBankItems";

    private const int RETAIL_CAPACITY = 100;

    private static int s_warnedBank;
    private static int s_warnedShared;

    /// <summary>
    /// The default capacity of a character's bank. The retail 100 when the key is unset, empty or not a positive number.
    /// </summary>
    internal static int MaxBankItems => Capacity(MAX_BANK_ITEMS_KEY, ref s_warnedBank);

    /// <summary>
    /// The default capacity of an account's shared bank. The retail 100 when the key is unset, empty or not a positive number.
    /// </summary>
    internal static int MaxSharedBankItems => Capacity(MAX_SHARED_BANK_ITEMS_KEY, ref s_warnedShared);

    /// <summary>
    /// The most jewels the shared bank holds. Zero when the key is unset, which keeps jewels out of the shared bank.
    /// </summary>
    internal static int SharedJewelCap => Math.Max(0, ConfigurationManager.GetValue(SHARED_JEWEL_CAP_KEY, 0));

    /// <summary>
    /// How much capacity one Expand Bank Elixir adds. Zero when the key is unset, which keeps the bank from being expanded.
    /// </summary>
    internal static int ExpandElixirAmount => Math.Max(0, ConfigurationManager.GetValue(EXPAND_ELIXIR_AMOUNT_KEY, 0));

    /// <summary>
    /// The largest capacity an expansion can reach. Zero when the key is unset, which means no limit.
    /// </summary>
    internal static int MaxBankLimit => Math.Max(0, ConfigurationManager.GetValue(MAX_BANK_LIMIT_KEY, 0));

    /// <summary>
    /// The object names of the NPCs that open the bank.
    /// </summary>
    internal static IReadOnlySet<string> ObjectNames {
        get {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configured = ConfigurationManager.GetValue(OBJECT_NAMES_KEY, string.Empty);
            foreach (var name in configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                names.Add(name);
            }

            return names;
        }
    }

    /// <summary>
    /// Whether the bank is on: both default capacities are above 0.
    /// </summary>
    internal static bool IsEnabled => MaxBankItems > 0 && MaxSharedBankItems > 0;

    private static int Capacity(string key, ref int warned) {
        var configured = ConfigurationManager.GetValue(key, 0);
        if (configured > 0) {
            return configured;
        }

        if (Interlocked.Exchange(ref warned, 1) == 0) {
            Logger.Warning("[Character] {Key} is unset, empty or 0 or less, so the retail capacity {Capacity} is used.",
                Logger.Args(key, RETAIL_CAPACITY));
        }

        return RETAIL_CAPACITY;
    }

}
