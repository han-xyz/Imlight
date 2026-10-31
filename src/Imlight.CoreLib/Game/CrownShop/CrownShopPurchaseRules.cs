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
 * CROWN SHOP PURCHASE RULES
 * ========================================================================
 * 
 * PURPOSE:
 * The decisions a crown shop purchase request goes through before any currency moves.
 * 
 * USAGE EXAMPLE:
 * CrownShopService.ReceivePurchaseRequest prices a request with TryPrice and charges a buyer's own purchase
 * through ChargeUnlessDuplicateRental.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Rentals;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.CrownShop;

internal static class CrownShopPurchaseRules {

    /// <summary>
    /// Prices a request. A request without a price is refused and logged, and leaves no record on the account:
    /// an honest player reaches this from a catalogue that a reload changed while they were online.
    /// </summary>
    public static bool TryPrice(Wizard wizard, CrownShopItem item, ulong itemId, int currency, int count, out int total) {
        if (CrownShopPrice.TryCompute(item, currency, count, out total)) {
            return true;
        }

        Logger.Warning("{Character} (ID {CharId}) tried to buy crown shop item {Template} "
            + "with currency type {CurrencyType} (count {Count}), which the item isn't sold for. "
            + "The purchase was refused.",
            Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), wizard.CharId, itemId, currency, count));
        return false;
    }

    public static int ChargeUnlessDuplicateRental(Wizard buyer, CoreTemplate template, int count, Func<int> debit)
        => RentalItems.WouldDuplicateType(buyer, template, count) ? CrownShopErrors.PriceLockMismatch : debit();

}
