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
 * HENCHMAN PURCHASE
 * ========================================================================
 * 
 * PURPOSE:
 * A henchman is a crown shop purchase that summons a creature into the buyer's duel,
 * never an inventory item. The crowns are taken only after the summon succeeded.
 * 
 * USAGE EXAMPLE:
 * CrownShopService.ReceivePurchaseRequest routes with Route, asks the duel to summon
 * the creature from TryGetCreatureTemplateId, and ends the purchase with Complete.
 * 
 * NOTE:
 * The creature is named by the item's ElixirBehaviorTemplate: m_equipActionList holds a
 * ResSummonHenchman whose m_templateID is the creature template (Henchman-T18-Death 1582001
 * summons 1588709). CombatDuelComponent.SummonHenchman does the summon.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.CrownShop;

public enum CrownShopPurchaseRoute {
    Grant,
    SummonHenchman,
    RefuseInCombat,
    RefuseNotInCombat,
}

public enum HenchmanSummonResult {
    Summoned,
    NoFreeSlot,
    NotInDuel,
    Failed,
    InPvP,
    NotPlanningPhase,
}

internal static class HenchmanPurchase {

    private static readonly int s_errorSigilFull = (int) StringHash.Compute("Error_HenchmenSigilFull");
    private static readonly int s_errorNotInCombat = (int) StringHash.Compute("Error_NotInCombat");
    private static readonly int s_errorPurchase = (int) StringHash.Compute("Error_PriceLockMismatch");
    private static readonly int s_errorInPvP = (int) StringHash.Compute("Error_HenchmenInPVP");
    private static readonly int s_errorPlanningPhase = (int) StringHash.Compute("Error_HenchmenPlanningPhase");

    public static CrownShopPurchaseRoute Route(bool isInDuel, bool isCombatOnly, bool isHenchman) {
        if (!isInDuel) {
            return isCombatOnly ? CrownShopPurchaseRoute.RefuseNotInCombat : CrownShopPurchaseRoute.Grant;
        }

        if (isHenchman) {
            return CrownShopPurchaseRoute.SummonHenchman;
        }

        return isCombatOnly ? CrownShopPurchaseRoute.Grant : CrownShopPurchaseRoute.RefuseInCombat;
    }

    public static bool TryGetCreatureTemplateId(CoreTemplate itemTemplate, out uint creatureTemplateId) {
        var summon = itemTemplate?.m_behaviors
            .OfType<ElixirBehaviorTemplate>()
            .SelectMany(elixir => elixir.m_equipActionList?.m_results ?? [])
            .OfType<ResSummonHenchman>()
            .FirstOrDefault();

        creatureTemplateId = (uint) (summon?.m_templateID.Full ?? 0);
        return creatureTemplateId != 0;
    }

    public static bool IsPvPEnabled(CoreTemplate itemTemplate)
        => itemTemplate?.m_behaviors.OfType<ElixirBehaviorTemplate>().Any(elixir => elixir.m_PvPEnabled) == true;

    /// <summary>
    /// Why the duel refuses a henchman before any slot is looked at, or null when it may summon.
    /// </summary>
    public static HenchmanSummonResult? Refusal(bool isPvPDuel, bool isPvPEnabled, bool isPlanningPhase) {
        if (isPvPDuel && !isPvPEnabled) {
            return HenchmanSummonResult.InPvP;
        }

        return isPlanningPhase ? null : HenchmanSummonResult.NotPlanningPhase;
    }

    /// <summary>
    /// Runs <paramref name="debit"/> only when the henchman was summoned.
    /// </summary>
    /// <returns>0 on success, the client error for a refused summon, or the error from <paramref name="debit"/>.</returns>
    public static int Complete(HenchmanSummonResult result, Func<int> debit) => result switch {
        HenchmanSummonResult.Summoned => debit(),
        HenchmanSummonResult.NoFreeSlot => s_errorSigilFull,
        HenchmanSummonResult.NotInDuel => s_errorNotInCombat,
        HenchmanSummonResult.InPvP => s_errorInPvP,
        HenchmanSummonResult.NotPlanningPhase => s_errorPlanningPhase,
        _ => s_errorPurchase,
    };

}
