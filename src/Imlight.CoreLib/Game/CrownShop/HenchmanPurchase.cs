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
 * never an inventory item. The payment is taken before the summon and refunded unless it succeeded.
 * 
 * USAGE EXAMPLE:
 * CrownShopService.ReceivePurchaseRequest routes with Route, asks the duel to summon
 * the creature from TryGetCreatureTemplateId, and settles it through one HenchmanPurchaseFlow per session.
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

    private static readonly int s_errorSigilFull = CrownShopErrors.Register("Error_HenchmenSigilFull");
    private static readonly int s_errorInPvP = CrownShopErrors.Register("Error_HenchmenInPVP");
    private static readonly int s_errorPlanningPhase = CrownShopErrors.Register("Error_HenchmenPlanningPhase");

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

    public static HenchmanSummonResult? Refusal(bool isPvPDuel, bool isPvPEnabled, bool isPlanningPhase) {
        // Checked before any slot is looked at; null means the duel may summon.
        if (isPvPDuel && !isPvPEnabled) {
            return HenchmanSummonResult.InPvP;
        }

        return isPlanningPhase ? null : HenchmanSummonResult.NotPlanningPhase;
    }

    internal static int PurchaseError => CrownShopErrors.PriceLockMismatch;

    internal static int ErrorFor(HenchmanSummonResult result) => result switch {
        HenchmanSummonResult.Summoned => 0,
        HenchmanSummonResult.NoFreeSlot => s_errorSigilFull,
        HenchmanSummonResult.NotInDuel => CrownShopErrors.NotInCombat,
        HenchmanSummonResult.InPvP => s_errorInPvP,
        HenchmanSummonResult.NotPlanningPhase => s_errorPlanningPhase,
        _ => CrownShopErrors.PriceLockMismatch,
    };

}

// One session's henchman purchase in flight: paid before the summon is asked for, refunded unless it succeeds.
internal sealed class HenchmanPurchaseFlow {

    private ulong _pendingPurchaseId;
    private System.Action _refund;

    public bool IsPending => _pendingPurchaseId != 0;

    public int Begin(ulong purchaseId, Func<int> debit, System.Action refund) {
        if (IsPending) {
            return HenchmanPurchase.PurchaseError;
        }

        var debitError = debit();
        if (debitError != 0) {
            return debitError;
        }

        _pendingPurchaseId = purchaseId;
        _refund = refund;
        return 0;
    }

    public bool TryFinish(ulong purchaseId, HenchmanSummonResult result, out int error) {
        error = 0;
        // An answer for a purchase already settled (timed out or abandoned) must not refund twice.
        if (!IsPending || purchaseId != _pendingPurchaseId) {
            return false;
        }

        var refund = Settle();
        if (result != HenchmanSummonResult.Summoned) {
            refund();
            error = HenchmanPurchase.ErrorFor(result);
        }

        return true;
    }

    public bool TryAbandon() {
        if (!IsPending) {
            return false;
        }

        Settle()();
        return true;
    }

    private System.Action Settle() {
        var refund = _refund;
        _pendingPurchaseId = 0;
        _refund = null;
        return refund;
    }

}
