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
 * TIERED SPELL LEDGER TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for spellement upgrades: the upgrade decision table, refunds, multiple paths, reset and
 * which tier a wizard may cast.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj --filter TieredSpellLedgerTests
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
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.TieredSpells;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class TieredSpellLedgerTests {

    private const uint FireCatId = 100;
    private const uint FireCatTwoId = 200;
    private const uint FireCatThreeId = 300;
    private const uint ReturnRate = 100;
    private const uint StrayTierId = 777;
    private const int FireCatGroupIndex = 4;
    private const int OtherGroupIndex = 9;

    private static readonly IndexedTieredSpellGroup s_fireCatGroup = new("Fire Cat", 1492248,
        ["Fire Cat", "Fire Cat - T02", "Fire Cat - T02 - B", "Fire Cat - T03 - A"]);

    private static readonly TieredSpellTemplate s_fireCat = new() {
        m_name = "Fire Cat",
        m_nextTierSpells = ["Fire Cat - T02 - B", "Fire Cat - T02"],
    };

    private static readonly TieredSpellTemplate s_fireCatTwo = new() {
        m_name = "Fire Cat - T02",
        m_shardCost = 75,
        m_newShardCost = 50,
        m_nextTierSpells = ["Fire Cat - T03 - A"],
    };

    private static readonly TieredSpellTemplate s_fireCatThree = new() {
        m_name = "Fire Cat - T03 - A",
        m_shardCost = 115,
        m_newShardCost = 100,
    };

    [Theory]
    [InlineData(false, "Fire Cat - T02", -1, 50, TieredSpellUpgradeOutcome.NotTrained)]
    [InlineData(true, "Fire Cat - T03 - A", -1, 500, TieredSpellUpgradeOutcome.NotNextTier)]
    [InlineData(true, "Fire Cat - T02", 2, 500, TieredSpellUpgradeOutcome.RequirementFailed)]
    [InlineData(true, "Fire Cat - T02", -1, 49, TieredSpellUpgradeOutcome.NotEnoughShards)]
    [InlineData(true, "Fire Cat - T02", -1, 50, TieredSpellUpgradeOutcome.Upgraded)]
    public void Upgrade_DecisionTable(bool isTrained, string nextName, int failedRequirementIndex, int shardsOwned,
        TieredSpellUpgradeOutcome expected) {
        var next = new TieredSpellTemplate { m_name = nextName, m_shardCost = 75, m_newShardCost = 50 };

        var decision = TieredSpellLedger.DecideUpgrade(s_fireCatGroup, isTrained, s_fireCat, next, failedRequirementIndex, shardsOwned);

        Assert.Equal(expected, decision.Outcome);
        Assert.Equal(expected == TieredSpellUpgradeOutcome.RequirementFailed ? 2 : TieredSpellLedger.NoFailedRequirement,
            decision.FailedRequirementIndex);
    }

    [Fact]
    public void Upgrade_RetiredTier_Refused() {
        var retired = new TieredSpellTemplate { m_name = "Fire Cat - T02", m_newShardCost = 50, m_retired = true };

        var decision = TieredSpellLedger.DecideUpgrade(s_fireCatGroup, true, s_fireCat, retired, TieredSpellLedger.NoFailedRequirement, 500);

        Assert.Equal(TieredSpellUpgradeOutcome.Retired, decision.Outcome);
    }

    [Fact]
    public void Upgrade_Success_DebitsTheNewShardCostOnce() {
        var decision = TieredSpellLedger.DecideUpgrade(s_fireCatGroup, true, s_fireCat, s_fireCatTwo, TieredSpellLedger.NoFailedRequirement, 80);
        var progress = NewProgress();

        Assert.True(TieredSpellLedger.ApplyUpgrade(progress, FireCatId, FireCatTwoId, decision.Cost));

        Assert.Equal(50, decision.Cost);
        Assert.Equal(FireCatTwoId, progress.CurrentSpellId);
        Assert.Equal([new TieredSpellUnlock { SpellId = FireCatTwoId, FromSpellId = FireCatId, ShardsSpent = 50 }],
            progress.Unlocks, UnlockComparer.Instance);
    }

    [Fact]
    public void Upgrade_TierOfAnotherGroup_Refused() {
        var scarab = new TieredSpellTemplate { m_name = "Scarab", m_nextTierSpells = ["Scarab - T02 - A"] };
        var scarabTwo = new TieredSpellTemplate { m_name = "Scarab - T02 - A", m_newShardCost = 50 };
        var foreignNext = new TieredSpellTemplate { m_name = "Fire Cat - T02", m_newShardCost = 50 };
        scarab.m_nextTierSpells.Add("Fire Cat - T02");

        var bothForeign = TieredSpellLedger.DecideUpgrade(s_fireCatGroup, true, scarab, scarabTwo,
            TieredSpellLedger.NoFailedRequirement, 500);
        var currentForeign = TieredSpellLedger.DecideUpgrade(s_fireCatGroup, true, scarab, foreignNext,
            TieredSpellLedger.NoFailedRequirement, 500);

        Assert.Equal(TieredSpellUpgradeOutcome.WrongGroup, bothForeign.Outcome);
        Assert.Equal(TieredSpellUpgradeOutcome.WrongGroup, currentForeign.Outcome);
        Assert.Equal(0, bothForeign.Cost);
    }

    [Theory]
    [InlineData("Fire Cat", "Fire Cat - T02", 1)]
    [InlineData("Scarab", "Scarab - T02 - A", 2)]
    [InlineData("Scarab", "Fire Cat - T02", TieredSpellLedger.NoGroup)]
    [InlineData("Unknown", "Fire Cat - T02", TieredSpellLedger.NoGroup)]
    public void ResolveGroup_BothTiersOfOneGroup(string currentName, string nextName, int expected) {
        IndexedTieredSpellGroup[] groups = [
            new("Frost Beetle", 1494687, ["Frost Beetle"]),
            s_fireCatGroup,
            new("Scarab", 1494700, ["Scarab", "Scarab - T02 - A"]),
        ];

        Assert.Equal(expected, TieredSpellLedger.ResolveGroup(groups, currentName, nextName));
    }

    [Fact]
    public void ApplyUpgrade_NotFromTheCurrentTier_Refused() {
        var progress = UpgradedToThree();

        Assert.False(TieredSpellLedger.ApplyUpgrade(progress, FireCatId, 400, 50));

        Assert.Equal(FireCatThreeId, progress.CurrentSpellId);
        Assert.Equal(2, progress.Unlocks.Count);
    }

    [Fact]
    public void ApplyUpgrade_TierAlreadyUnlocked_Refused() {
        var progress = NewProgress();
        Assert.True(TieredSpellLedger.ApplyUpgrade(progress, FireCatId, FireCatTwoId, 50));

        Assert.False(TieredSpellLedger.ApplyUpgrade(progress, FireCatTwoId, FireCatTwoId, 50));
        Assert.False(TieredSpellLedger.ApplyUpgrade(progress, FireCatTwoId, FireCatId, 50));

        Assert.Single(progress.Unlocks);
    }

    [Fact]
    public void FirstFailedRequirement_ReportsTheFailingIndex() {
        var passing = new Requirement();
        var failing = new Requirement();
        var negated = new Requirement { m_applyNOT = true };
        var list = new RequirementList { m_requirements = [passing, negated, failing] };

        var index = TieredSpellLedger.FirstFailedRequirement(list, requirement => requirement != failing && requirement != negated);

        Assert.Equal(2, index);
        Assert.Equal(1, TieredSpellLedger.FirstFailedRequirement(list, requirement => requirement != failing));
        Assert.Equal(TieredSpellLedger.NoFailedRequirement,
            TieredSpellLedger.FirstFailedRequirement(list, requirement => requirement != negated));
        Assert.Equal(TieredSpellLedger.NoFailedRequirement, TieredSpellLedger.FirstFailedRequirement(null, _ => false));
    }

    [Theory]
    [InlineData(100u, 150)]
    [InlineData(50u, 75)]
    public void Refund_ReturnsTheRateOfTheShardsSpentAbove(uint returnRate, int expected) {
        var progress = UpgradedToThree();

        Assert.True(TieredSpellLedger.TryRefund(progress, FireCatThreeId, FireCatId, returnRate, out var returned));

        Assert.Equal(expected, returned);
        Assert.Equal(FireCatId, progress.CurrentSpellId);
        Assert.Empty(progress.Unlocks);
    }

    [Fact]
    public void Refund_OneTier_KeepsTheTierBelow() {
        var progress = UpgradedToThree();

        Assert.True(TieredSpellLedger.TryRefund(progress, FireCatThreeId, FireCatTwoId, ReturnRate, out var returned));

        Assert.Equal(100, returned);
        Assert.Equal(FireCatTwoId, progress.CurrentSpellId);
        Assert.Single(progress.Unlocks);
    }

    [Fact]
    public void Refund_TargetNotBelowTheCurrentTier_Refused() {
        var progress = UpgradedToThree();

        Assert.False(TieredSpellLedger.TryRefund(progress, FireCatTwoId, FireCatThreeId, ReturnRate, out var returned));
        Assert.False(TieredSpellLedger.TryRefund(progress, FireCatThreeId, 999, ReturnRate, out _));

        Assert.Equal(0, returned);
        Assert.Equal(FireCatThreeId, progress.CurrentSpellId);
        Assert.Equal(2, progress.Unlocks.Count);
    }

    [Fact]
    public void Refund_Twice_Refused() {
        var progress = UpgradedToThree();
        Assert.True(TieredSpellLedger.TryRefund(progress, FireCatThreeId, FireCatTwoId, ReturnRate, out _));

        Assert.False(TieredSpellLedger.TryRefund(progress, FireCatThreeId, FireCatTwoId, ReturnRate, out var returned));

        Assert.Equal(0, returned);
        Assert.Equal(FireCatTwoId, progress.CurrentSpellId);
    }

    [Fact]
    public void Refund_RateRoundsDown() {
        var progress = UpgradedToThree();

        Assert.True(TieredSpellLedger.TryRefund(progress, FireCatThreeId, FireCatId, 33, out var returned));

        Assert.Equal(49, returned);
    }

    [Fact]
    public void Copy_ChangesLeaveTheOriginalUntouched() {
        var progress = UpgradedToThree();

        var draft = TieredSpellLedger.Copy(progress);
        Assert.True(TieredSpellLedger.TryRefundSpells(draft, [FireCatTwoId, FireCatThreeId], ReturnRate, out _));

        Assert.Equal(FireCatThreeId, progress.CurrentSpellId);
        Assert.Equal(2, progress.Unlocks.Count);
        Assert.Equal(FireCatId, draft.CurrentSpellId);
    }

    [Fact]
    public void RemovedTiers_AreTheTiersLeftOffThePath() {
        var progress = UpgradedToThree();
        var draft = TieredSpellLedger.Copy(progress);
        Assert.True(TieredSpellLedger.TryRefund(draft, FireCatThreeId, FireCatId, ReturnRate, out _));

        Assert.Equal([FireCatTwoId, FireCatThreeId], TieredSpellLedger.RemovedTiers(progress, draft).Order());
        Assert.Empty(TieredSpellLedger.RemovedTiers(progress, progress));
    }

    [Fact]
    public void ReplacedDeckCards_OnlyRemovedTiersWithTheirCopies() {
        List<SpellData> deck = [
            new() { m_templateID = FireCatThreeId, m_quantity = 2 },
            new() { m_templateID = 555, m_quantity = 4 },
            new() { m_templateID = FireCatTwoId, m_quantity = 1 },
        ];

        var replaced = TieredSpellLedger.ReplacedDeckCards(deck, new HashSet<uint> { FireCatTwoId, FireCatThreeId });

        Assert.Equal([(FireCatThreeId, 2), (FireCatTwoId, 1)], replaced);
    }

    [Fact]
    public void NewProgress_StartsAtTierOneOnly() {
        var progress = TieredSpellLedger.NewProgress(FireCatGroupIndex, FireCatId);

        Assert.False(TieredSpellLedger.CanApplyUpgrade(progress, FireCatTwoId, FireCatThreeId));
        Assert.True(TieredSpellLedger.CanApplyUpgrade(progress, FireCatId, FireCatTwoId));
        Assert.False(progress.DonePrepForMultiplePaths);
    }

    [Theory]
    [InlineData(FireCatId, FireCatGroupIndex, FireCatTwoId, FireCatGroupIndex, FireCatTwoId)]
    [InlineData(FireCatTwoId, FireCatGroupIndex, 0u, TieredSpellLedger.NoGroup, FireCatTwoId)]
    [InlineData(FireCatThreeId, FireCatGroupIndex, 0u, TieredSpellLedger.NoGroup, null)]
    [InlineData(FireCatThreeId, FireCatGroupIndex, FireCatTwoId, FireCatGroupIndex, null)]
    [InlineData(FireCatId, FireCatGroupIndex, FireCatThreeId, FireCatGroupIndex, FireCatId)]
    [InlineData(FireCatId, FireCatGroupIndex, 888u, OtherGroupIndex, FireCatId)]
    [InlineData(888u, OtherGroupIndex, 0u, TieredSpellLedger.NoGroup, 888u)]
    [InlineData(555u, TieredSpellLedger.NoGroup, FireCatTwoId, FireCatGroupIndex, 555u)]
    public void CastTier_DealtAndSelectedMustBeTrained(uint dealt, int dealtGroup, uint selected, int selectedGroup,
        uint? expected) {
        var progress = TieredSpellLedger.NewProgress(FireCatGroupIndex, FireCatId);
        Assert.True(TieredSpellLedger.ApplyUpgrade(progress, FireCatId, FireCatTwoId, 50));

        Assert.Equal(expected, TieredSpellLedger.CastTier([progress], dealt, dealtGroup, selected, selectedGroup));
    }

    [Fact]
    public void SwapSpell_NewTierNotLearned_KeepsTheOldOne() {
        var unlearned = false;

        Assert.False(TieredSpellLedger.TrySwapSpell(() => false, () => unlearned = true));
        Assert.False(unlearned);

        Assert.True(TieredSpellLedger.TrySwapSpell(() => true, () => unlearned = true));
        Assert.True(unlearned);
    }

    [Fact]
    public void CastTier_WithoutProgress_CastsTheDealtCard() {
        Assert.Equal(FireCatThreeId,
            TieredSpellLedger.CastTier(null, FireCatThreeId, FireCatGroupIndex, FireCatTwoId, FireCatGroupIndex));
    }

    [Fact]
    public void RefundForMultiplePaths_ListedTopTiers_Refunded() {
        var progress = UpgradedToThree();

        Assert.True(TieredSpellLedger.TryRefundSpells(progress, [FireCatTwoId, FireCatThreeId], ReturnRate, out var returned));

        Assert.Equal(150, returned);
        Assert.Equal(FireCatId, progress.CurrentSpellId);
        Assert.Empty(progress.Unlocks);
    }

    [Fact]
    public void RefundForMultiplePaths_ListNotTheTopOfThePath_Refused() {
        var progress = UpgradedToThree();

        Assert.False(TieredSpellLedger.TryRefundSpells(progress, [FireCatTwoId], ReturnRate, out _));
        Assert.False(TieredSpellLedger.TryRefundSpells(progress, [], ReturnRate, out _));

        Assert.Equal(FireCatThreeId, progress.CurrentSpellId);
    }

    [Theory]
    [InlineData(true, FireCatThreeId, 0)]
    [InlineData(false, FireCatId, 150)]
    public void PrepForMultiplePaths_KeepsOrResets(bool keepProgression, uint expectedSpell, int expectedReturned) {
        var progress = UpgradedToThree();

        Assert.True(TieredSpellLedger.TryPrepForMultiplePaths(progress, keepProgression, ReturnRate, out var returned));

        Assert.Equal(expectedReturned, returned);
        Assert.Equal(expectedSpell, progress.CurrentSpellId);
        Assert.True(progress.DonePrepForMultiplePaths);
    }

    [Fact]
    public void PrepForMultiplePaths_Twice_Refused() {
        var progress = UpgradedToThree();
        Assert.True(TieredSpellLedger.TryPrepForMultiplePaths(progress, true, ReturnRate, out _));

        Assert.False(TieredSpellLedger.TryPrepForMultiplePaths(progress, false, ReturnRate, out var returned));

        Assert.Equal(0, returned);
        Assert.Equal(FireCatThreeId, progress.CurrentSpellId);
        Assert.Equal(2, progress.Unlocks.Count);
    }

    [Fact]
    public void Reset_ReturnsToTierOneWithEveryShard() {
        var progress = UpgradedToThree();

        Assert.Equal(150, TieredSpellLedger.Reset(progress, ReturnRate));

        Assert.Equal(FireCatId, progress.CurrentSpellId);
        Assert.Empty(progress.Unlocks);
    }

    [Theory]
    [InlineData(FireCatId, true)]
    [InlineData(FireCatTwoId, true)]
    [InlineData(FireCatThreeId, false)]
    [InlineData(999u, false)]
    [InlineData(StrayTierId, false)]
    public void CastTier_AcceptedOnlyForTrainedTiers(uint selected, bool expected) {
        var progress = NewProgress();
        TieredSpellLedger.ApplyUpgrade(progress, FireCatId, FireCatTwoId, 50);
        progress.Unlocks.Add(new TieredSpellUnlock { SpellId = StrayTierId, FromSpellId = 999, ShardsSpent = 50 });

        Assert.Equal(expected, TieredSpellLedger.IsTrainedTier([progress], selected));
    }

    private static TieredSpellProgress NewProgress()
        => new() { GroupIndex = 4, TierOneSpellId = FireCatId, CurrentSpellId = FireCatId };

    private static TieredSpellProgress UpgradedToThree() {
        var progress = NewProgress();
        TieredSpellLedger.ApplyUpgrade(progress, FireCatId, FireCatTwoId, s_fireCatTwo.m_newShardCost);
        TieredSpellLedger.ApplyUpgrade(progress, FireCatTwoId, FireCatThreeId, s_fireCatThree.m_newShardCost);

        return progress;
    }

    private sealed class UnlockComparer : IEqualityComparer<TieredSpellUnlock> {

        internal static readonly UnlockComparer Instance = new();

        public bool Equals(TieredSpellUnlock? x, TieredSpellUnlock? y)
            => x?.SpellId == y?.SpellId && x?.FromSpellId == y?.FromSpellId && x?.ShardsSpent == y?.ShardsSpent;

        public int GetHashCode(TieredSpellUnlock obj) => obj.SpellId.GetHashCode();

    }

}
