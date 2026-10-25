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
 * CROWN SHOP PRICE
 * ========================================================================
 * 
 * PURPOSE:
 * The total a crown shop purchase costs in the currency the client chose,
 * from the catalogue price alone.
 * 
 * USAGE EXAMPLE:
 * CrownShopService.ReceivePurchaseRequest prices a MSG_PCS_PURCHASE_REQUEST before debiting.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.CrownShop;

internal static class CrownShopPrice {

    public const int GoldCurrency = 0;
    public const int CrownsCurrency = 1;

    /// <summary>
    /// False when the item has no price in <paramref name="currency"/> (a zero price means
    /// the item is not sold for it) or the total does not fit in an int.
    /// </summary>
    public static bool TryCompute(CrownShopItem item, int currency, int count, out int total) {
        var unitPrice = currency switch {
            GoldCurrency => item.m_goldCost,
            CrownsCurrency => item.m_crownsCost,
            _ => 0,
        };

        var longTotal = (long) unitPrice * count;
        if (unitPrice <= 0 || count <= 0 || longTotal > int.MaxValue) {
            total = 0;
            return false;
        }

        total = (int) longTotal;
        return true;
    }

}
