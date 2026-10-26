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
 * HENCHMAN PURCHASE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for buying a henchman in a duel: which purchases summon,
 * which item names the creature, when the crowns are taken, who may dismiss it.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Generic;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Zone.Components;
using Xunit;

namespace Imlight.Tests;

public sealed class HenchmanPurchaseTests {

    private const uint CreatureTemplateId = 1588709;
    private const int DebitError = 9;
    private const ulong PurchaseId = 77;

    [Theory]
    [InlineData(true, true, true, CrownShopPurchaseRoute.SummonHenchman)]
    [InlineData(true, false, false, CrownShopPurchaseRoute.RefuseInCombat)]
    [InlineData(false, true, true, CrownShopPurchaseRoute.RefuseNotInCombat)]
    [InlineData(true, true, false, CrownShopPurchaseRoute.Grant)]
    [InlineData(false, false, false, CrownShopPurchaseRoute.Grant)]
    public void Route_FollowsDuelStateAndItemKind(bool isInDuel, bool isCombatOnly, bool isHenchman,
                                                  CrownShopPurchaseRoute expected) {
        Assert.Equal(expected, HenchmanPurchase.Route(isInDuel, isCombatOnly, isHenchman));
    }

    [Fact]
    public void CreatureTemplate_IsNamedByTheElixirSummonResult() {
        var item = new WizItemTemplate {
            m_behaviors = [
                new CrownShopButtonNameBehaviorTemplate(),
                new ElixirBehaviorTemplate {
                    m_equipActionList = new ResultList { m_results = [new ResSummonHenchman { m_templateID = CreatureTemplateId }] }
                },
            ]
        };

        Assert.True(HenchmanPurchase.TryGetCreatureTemplateId(item, out var creatureTemplateId));
        Assert.Equal(CreatureTemplateId, creatureTemplateId);
    }

    [Fact]
    public void CreatureTemplate_ItemWithoutSummonResult_IsNotFound() {
        var item = new WizItemTemplate { m_behaviors = [new ElixirBehaviorTemplate { m_equipActionList = new ResultList { m_results = [] } }] };

        Assert.False(HenchmanPurchase.TryGetCreatureTemplateId(item, out _));
    }

    [Theory]
    [InlineData(false, false, true, null)]
    [InlineData(true, true, true, null)]
    [InlineData(true, false, true, HenchmanSummonResult.InPvP)]
    [InlineData(false, false, false, HenchmanSummonResult.NotPlanningPhase)]
    [InlineData(true, false, false, HenchmanSummonResult.InPvP)]
    public void Refusal_FollowsPvPAndDuelPhase(bool isPvPDuel, bool isPvPEnabled, bool isPlanningPhase,
                                               HenchmanSummonResult? expected) {
        Assert.Equal(expected, HenchmanPurchase.Refusal(isPvPDuel, isPvPEnabled, isPlanningPhase));
    }

    [Fact]
    public void PvPEnabled_IsReadFromTheElixir() {
        var item = new WizItemTemplate { m_behaviors = [new ElixirBehaviorTemplate { m_PvPEnabled = true }] };

        Assert.True(HenchmanPurchase.IsPvPEnabled(item));
        Assert.False(HenchmanPurchase.IsPvPEnabled(new WizItemTemplate { m_behaviors = [new ElixirBehaviorTemplate()] }));
    }

    [Theory]
    [InlineData(HenchmanSummonResult.NoFreeSlot, "Error_HenchmenSigilFull")]
    [InlineData(HenchmanSummonResult.InPvP, "Error_HenchmenInPVP")]
    [InlineData(HenchmanSummonResult.NotPlanningPhase, "Error_HenchmenPlanningPhase")]
    [InlineData(HenchmanSummonResult.NotInDuel, "Error_NotInCombat")]
    [InlineData(HenchmanSummonResult.Failed, "Error_PriceLockMismatch")]
    public void SummonRefused_DebitsThenRefunds(HenchmanSummonResult result, string expectedError) {
        var flow = new HenchmanPurchaseFlow();
        var events = new List<string>();

        Assert.Equal(0, flow.Begin(PurchaseId, () => {
            events.Add("debit");
            return 0;
        }, () => events.Add("refund")));
        Assert.True(flow.TryFinish(PurchaseId, result, out var error));

        Assert.Equal(["debit", "refund"], events);
        Assert.Equal((int) StringHash.Compute(expectedError), error);
        Assert.False(flow.IsPending);
    }

    [Fact]
    public void Summoned_KeepsTheDebit() {
        var flow = new HenchmanPurchaseFlow();
        var refunds = 0;
        flow.Begin(PurchaseId, () => 0, () => refunds++);

        Assert.True(flow.TryFinish(PurchaseId, HenchmanSummonResult.Summoned, out var error));

        Assert.Equal(0, error);
        Assert.Equal(0, refunds);
    }

    [Fact]
    public void DebitRefused_SendsNoSummonAndLeavesNothingPending() {
        var flow = new HenchmanPurchaseFlow();
        var refunds = 0;

        Assert.Equal(DebitError, flow.Begin(PurchaseId, () => DebitError, () => refunds++));
        Assert.False(flow.IsPending);
        Assert.False(flow.TryAbandon());
        Assert.Equal(0, refunds);
    }

    [Fact]
    public void SecondRequestWhilePending_IsRefusedWithoutDebit() {
        var flow = new HenchmanPurchaseFlow();
        var debits = 0;
        flow.Begin(PurchaseId, () => ++debits * 0, () => { });

        var error = flow.Begin(PurchaseId + 1, () => ++debits * 0, () => { });

        Assert.Equal((int) StringHash.Compute("Error_PriceLockMismatch"), error);
        Assert.Equal(1, debits);
    }

    [Fact]
    public void AnswerForAnotherPurchase_IsIgnoredAndRefundsNothing() {
        var flow = new HenchmanPurchaseFlow();
        var refunds = 0;
        flow.Begin(PurchaseId, () => 0, () => refunds++);

        Assert.False(flow.TryFinish(PurchaseId + 1, HenchmanSummonResult.NoFreeSlot, out _));

        Assert.Equal(0, refunds);
        Assert.True(flow.IsPending);
    }

    [Fact]
    public void SessionClosesWhilePending_RefundsOnceAndIgnoresTheLateAnswer() {
        var flow = new HenchmanPurchaseFlow();
        var refunds = 0;
        flow.Begin(PurchaseId, () => 0, () => refunds++);

        Assert.True(flow.TryAbandon());
        Assert.False(flow.TryFinish(PurchaseId, HenchmanSummonResult.NoFreeSlot, out _));

        Assert.Equal(1, refunds);
        Assert.False(flow.IsPending);
    }

    [Theory]
    [InlineData(true, 4, 4, true)]
    [InlineData(true, 5, 4, false)]
    [InlineData(false, 4, 4, false)]
    public void Dismiss_OnlyTheOwnersMinion(bool isSummonedMinion, int minionOwnerSlot, int requesterSlot, bool expected) {
        Assert.Equal(expected, CombatDuelComponent.MayDismiss(isSummonedMinion, minionOwnerSlot, requesterSlot));
    }

}
