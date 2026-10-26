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
 * BACKPACK EXPANSION ELIXIR
 * ========================================================================
 * 
 * PURPOSE:
 * The Expand Backpack Elixir permanently adds backpack slots to the wizard who uses it, up to a limit.
 * 
 * USAGE EXAMPLE:
 * EquipmentService and CrownShopService call TryUse/TryExpand ahead of their instant-elixir branch;
 * the crown shop refuses a purchase that MayPurchase rejects before it debits.
 * 
 * NOTE:
 * The elixir is ObjectData/Elixirs/Elixir-041.xml (WizardElixirs_00000057). Its equip result is a
 * server-only type the client type dump does not name, so it is recognised by template id.
 * The increment and the limit are in neither the template nor the crown-shop data; they follow
 * retail (80 base slots, 130 at most).
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Elixirs;

internal enum BackpackExpansionResult {
    Expanded,
    AtLimit,
    NotInBackpack,
    SaveFailed
}

internal static class BackpackExpansion {

    public const ulong ElixirTemplateId = 1431039;
    public const int SlotsPerElixir = 25;
    public const int MaxElixirs = 2;

    private const int MaxExtraSpace = SlotsPerElixir * MaxElixirs;

    public static bool IsBackpackExpansion(ulong templateId) => templateId == ElixirTemplateId;

    public static bool IsAtLimit(Wizard wizard) => wizard.InventoryBehavior.ExtraSpace >= MaxExtraSpace;

    public static bool MayPurchase(Wizard wizard, int count)
        => wizard.InventoryBehavior.ExtraSpace + count * SlotsPerElixir <= MaxExtraSpace;

    /// <summary>
    /// Adds one elixir's slots to the wizard and stores them with <paramref name="save"/>. Unless the result is
    /// <see cref="BackpackExpansionResult.Expanded"/>, the wizard is left as it was.
    /// </summary>
    public static BackpackExpansionResult TryExpand(Wizard wizard, Func<bool> save) {
        if (IsAtLimit(wizard)) {
            return BackpackExpansionResult.AtLimit;
        }

        wizard.InventoryBehavior.ExtraSpace += SlotsPerElixir;
        if (save()) {
            return BackpackExpansionResult.Expanded;
        }

        wizard.InventoryBehavior.ExtraSpace -= SlotsPerElixir;
        return BackpackExpansionResult.SaveFailed;
    }

    /// <summary>
    /// Uses an elixir from the backpack: takes it out and adds its slots, and stores both in the one
    /// <paramref name="save"/>. Unless the result is <see cref="BackpackExpansionResult.Expanded"/>, the elixir
    /// stays in the backpack and the space is unchanged.
    /// </summary>
    public static BackpackExpansionResult TryUse(Wizard wizard, WizClientObjectItem elixir, Func<bool> save) {
        var inventory = wizard.InventoryBehavior;
        if (IsAtLimit(wizard)) {
            return BackpackExpansionResult.AtLimit;
        }

        if (!inventory.RemoveItem(elixir)) {
            return BackpackExpansionResult.NotInBackpack;
        }

        var result = TryExpand(wizard, save);
        if (result != BackpackExpansionResult.Expanded) {
            inventory.AddItem(elixir);
        }

        return result;
    }

    public static WIZARD2_53_PROTOCOL.MSG_UPDATEEXTRAINVENTORY ExtraInventoryUpdate(Wizard wizard)
        => new() {
            ExtraInventorySpace = wizard.InventoryBehavior.ExtraSpace,
            CurrentInventorySpace = wizard.InventoryBehavior.NumItemsAllowed
        };

}
