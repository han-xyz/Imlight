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
 * SPELLEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * The rules of spellement upgrades, refunds, the one-time reset for multiple paths and the tier a wizard may
 * cast, over a TieredSpellProgress and the tier templates. No I/O: TieredSpellService debits, grants and saves.
 *
 * USAGE EXAMPLE:
 * var decision = TieredSpellLedger.DecideUpgrade(isTrained, current, next, failedIndex, shardsOwned);
 *
 * NOTE:
 * The price is the target tier's m_newShardCost: m_shardCost is the price before Spellements 2.0, kept in the
 * data for the retired-spell totals (TieredSpellsRetired.xml sums both).
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
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.TieredSpells;

public enum TieredSpellUpgradeOutcome {
    Upgraded,
    NotTrained,
    NotNextTier,
    Retired,
    RequirementFailed,
    NotEnoughShards,
}

internal readonly record struct TieredSpellUpgradeDecision(TieredSpellUpgradeOutcome Outcome, int Cost, int FailedRequirementIndex);

internal static class TieredSpellLedger {

    internal const int NoFailedRequirement = -1;

    private const int PercentBase = 100;

    /// <summary>
    /// Decides an upgrade from the current tier to the next one.
    /// </summary>
    /// <param name="isTrained">Whether the wizard has the current tier in the spellbook.</param>
    /// <param name="current">The current tier.</param>
    /// <param name="next">The tier asked for.</param>
    /// <param name="failedRequirementIndex">The index of the next tier's first unmet requirement, or NoFailedRequirement.</param>
    /// <param name="shardsOwned">How many of the group's shards the wizard holds.</param>
    /// <returns>The outcome, the shards to debit and the failing requirement index.</returns>
    internal static TieredSpellUpgradeDecision DecideUpgrade(bool isTrained, TieredSpellTemplate current,
        TieredSpellTemplate next, int failedRequirementIndex, int shardsOwned) {
        var cost = next.m_newShardCost;
        var outcome = true switch {
            _ when !isTrained => TieredSpellUpgradeOutcome.NotTrained,
            _ when !IsNextTier(current, next) => TieredSpellUpgradeOutcome.NotNextTier,
            _ when next.m_retired => TieredSpellUpgradeOutcome.Retired,
            _ when failedRequirementIndex != NoFailedRequirement => TieredSpellUpgradeOutcome.RequirementFailed,
            _ when shardsOwned < cost => TieredSpellUpgradeOutcome.NotEnoughShards,
            _ => TieredSpellUpgradeOutcome.Upgraded,
        };

        return new(outcome,
            outcome == TieredSpellUpgradeOutcome.Upgraded ? cost : 0,
            outcome == TieredSpellUpgradeOutcome.RequirementFailed ? failedRequirementIndex : NoFailedRequirement);
    }

    /// <summary>
    /// Finds the first requirement of a list that is not met, each item judged on its own with its NOT applied.
    /// </summary>
    /// <param name="requirements">The requirement list, or null.</param>
    /// <param name="meets">Whether a single requirement, before its NOT, is met.</param>
    /// <returns>The index of the first unmet requirement, or NoFailedRequirement.</returns>
    internal static int FirstFailedRequirement(RequirementList requirements, Func<Requirement, bool> meets) {
        var items = requirements?.m_requirements ?? [];
        for (var index = 0; index < items.Count; index++) {
            if (items[index] is { } requirement && meets(requirement) == requirement.m_applyNOT) {
                return index;
            }
        }

        return NoFailedRequirement;
    }

    internal static void ApplyUpgrade(TieredSpellProgress progress, uint spellId, uint newSpellId, int cost) {
        progress.Unlocks.Add(new TieredSpellUnlock { SpellId = newSpellId, FromSpellId = spellId, ShardsSpent = cost });
        progress.CurrentSpellId = newSpellId;
    }

    /// <summary>
    /// Steps the current tier back down its path to a lower tier, returning the rate of the shards paid above it.
    /// </summary>
    /// <returns>False, with nothing changed, when currentSpellId is not the current tier or targetSpellId is not below it.</returns>
    internal static bool TryRefund(TieredSpellProgress progress, uint currentSpellId, uint targetSpellId, uint returnRate,
        out int shardsReturned) {
        shardsReturned = 0;
        if (currentSpellId != progress.CurrentSpellId || currentSpellId == targetSpellId) {
            return false;
        }

        var steps = PathDown(progress).TakeWhile(unlock => unlock.SpellId != targetSpellId).ToList();
        var reachesTarget = steps.Count > 0 && steps[^1].FromSpellId == targetSpellId;
        if (!reachesTarget) {
            return false;
        }

        shardsReturned = Remove(progress, steps, returnRate);
        return true;
    }

    /// <summary>
    /// Refunds the listed tiers when they are exactly the top of the current path.
    /// </summary>
    /// <returns>False, with nothing changed, when the list is empty or not the top of the path.</returns>
    internal static bool TryRefundSpells(TieredSpellProgress progress, IReadOnlyCollection<uint> spellIds, uint returnRate,
        out int shardsReturned) {
        shardsReturned = 0;
        var steps = PathDown(progress).Take(spellIds.Count).ToList();
        if (spellIds.Count == 0 || !steps.Select(unlock => unlock.SpellId).ToHashSet().SetEquals(spellIds)) {
            return false;
        }

        shardsReturned = Remove(progress, steps, returnRate);
        return true;
    }

    /// <summary>
    /// The one-time reset: keep the current tier, or return to tier one with the shards refunded.
    /// Either way the group is done with the prep.
    /// </summary>
    /// <returns>The shards to return.</returns>
    internal static int PrepForMultiplePaths(TieredSpellProgress progress, bool keepProgression, uint returnRate) {
        progress.DonePrepForMultiplePaths = true;

        return keepProgression ? 0 : Reset(progress, returnRate);
    }

    /// <summary>
    /// Returns the group to its tier-one spell.
    /// </summary>
    /// <returns>The shards to return.</returns>
    internal static int Reset(TieredSpellProgress progress, uint returnRate)
        => Remove(progress, [.. PathDown(progress)], returnRate);

    /// <summary>
    /// Whether a spell is a tier the wizard may cast: a group's tier-one spell or a tier bought on its path.
    /// </summary>
    internal static bool IsTrainedTier(IEnumerable<TieredSpellProgress> progress, uint spellId)
        => progress.Any(group => group.TierOneSpellId == spellId || group.Unlocks.Any(unlock => unlock.SpellId == spellId));

    private static bool IsNextTier(TieredSpellTemplate current, TieredSpellTemplate next)
        => (current.m_nextTierSpells ?? []).Any(name => name.ToString() == next.m_name.ToString());

    private static IEnumerable<TieredSpellUnlock> PathDown(TieredSpellProgress progress) {
        var spellId = progress.CurrentSpellId;
        while (progress.Unlocks.Find(unlock => unlock.SpellId == spellId) is { } unlock) {
            yield return unlock;
            spellId = unlock.FromSpellId;
        }
    }

    private static int Remove(TieredSpellProgress progress, IReadOnlyList<TieredSpellUnlock> steps, uint returnRate) {
        if (steps.Count == 0) {
            return 0;
        }

        foreach (var step in steps) {
            progress.Unlocks.Remove(step);
        }

        progress.CurrentSpellId = steps[^1].FromSpellId;

        return (int) (steps.Sum(step => (long) step.ShardsSpent) * returnRate / PercentBase);
    }

}
