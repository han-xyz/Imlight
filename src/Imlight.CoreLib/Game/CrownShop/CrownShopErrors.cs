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
 * CROWN SHOP ERRORS
 * ========================================================================
 * 
 * PURPOSE:
 * The client error codes a purchase can be refused with, hashed from their
 * Error.lang keys, and the key behind each code for the log.
 * 
 * USAGE EXAMPLE:
 * CrownShopService and HenchmanPurchase build their codes with Register;
 * SendPurchaseError logs NameOf(code).
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Concurrent;
using Imcodec.Cryptography;

namespace Imlight.CoreLib.Game.CrownShop;

internal static class CrownShopErrors {

    private static readonly ConcurrentDictionary<int, string> s_names = new();

    public static readonly int NotInCombat = Register("Error_NotInCombat");
    public static readonly int PriceLockMismatch = Register("Error_PriceLockMismatch");

    public static int Register(string name) {
        var code = (int) StringHash.Compute(name);
        s_names[code] = name;
        return code;
    }

    public static string NameOf(int code) => s_names.TryGetValue(code, out var name) ? name : code.ToString();

}
