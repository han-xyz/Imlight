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
 * PACK SLOT ODDS TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for crown pack slot odds: tier gates by slot name, the fallback chain, data overrides, pity and the
 * weighted item pick.
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

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.WizardData.Models.World;
using Xunit;

namespace Imlight.Tests;

public sealed class PackSlotOddsTests {

    private static readonly DropTable s_wildcard = Table("2023NM-PackLT-Wildcard");
    private static readonly DropTable s_rare = Table("2023NM-PackLT-Rare");
    private static readonly DropTable s_rare2 = Table("2023NM-PackLT-Rare-2");
    private static readonly DropTable s_uncommon = Table("2023NM-PackLT-Uncommon");
    private static readonly DropTable s_common = Table("2023NM-PackLT-Common");

    private static readonly IReadOnlyList<DropTable> s_nightMire =
        [s_wildcard, s_rare, s_rare2, s_uncommon, s_common, s_common, s_common];

    [Theory]
    [InlineData("2023NM-PackLT-Wildcard", "Wildcard")]
    [InlineData("2023NM-PackLT-Rare", "Rare")]
    [InlineData("2023NM-PackLT-Rare-2", "Rare")]
    [InlineData("2023NM-PackLT-Uncommon", "Uncommon")]
    [InlineData("2023NM-PackLT-Common", "Common")]
    public void TierOf_ReadsTheTierFromTheSlotName(string tableName, string expected)
        => Assert.Equal(expected, PackSlotOdds.TierOf(tableName).ToString());

    [Fact]
    public void TierOf_IsNullForAnUntieredTable()
        => Assert.Null(PackSlotOdds.TierOf("WC-Mob-Loot"));

    [Fact]
    public void DefaultChances_MatchTheConfiguredTiers() {
        var chances = PackTierChances.Default;

        Assert.Equal(0.02, chances.For(PackTier.Wildcard));
        Assert.Equal(0.06, chances.For(PackTier.Rare));
        Assert.Equal(0.30, chances.For(PackTier.Uncommon));
        Assert.Equal(1.0, chances.For(PackTier.Common));
    }

    [Fact]
    public void ResolveSlot_KeepsTheSlotTableWhenItsGatePasses()
        => Assert.Same(s_wildcard, Resolve(s_wildcard, s_nightMire, 0.01));

    [Fact]
    public void ResolveSlot_KeepsTheSecondRareTableForItsOwnSlot()
        => Assert.Same(s_rare2, Resolve(s_rare2, s_nightMire, 0.05));

    [Fact]
    public void ResolveSlot_FallsBackToTheFirstRareTable()
        => Assert.Same(s_rare, Resolve(s_wildcard, s_nightMire, 0.5, 0.05));

    [Fact]
    public void ResolveSlot_FallsDownTheWholeChainToCommon()
        => Assert.Same(s_common, Resolve(s_wildcard, s_nightMire, 0.5, 0.5, 0.5, 0.5));

    [Fact]
    public void ResolveSlot_SkipsATierThePackDoesNotCarry()
        => Assert.Same(s_common, Resolve(s_rare, [s_rare, s_common], 0.1, 0.01));

    [Fact]
    public void ResolveSlot_GrantsTheLowestTierThePackCarriesWithoutAGate()
        => Assert.Same(s_rare, Resolve(s_wildcard, [s_wildcard, s_rare], 0.5, 0.99));

    [Fact]
    public void ResolveSlot_LeavesAnUntieredSlotAlone() {
        var mobLoot = Table("WC-Mob-Loot");

        Assert.Same(mobLoot, Resolve(mobLoot, [mobLoot], 0.99));
    }

    [Fact]
    public void ResolveSlot_SkipsAnUntieredSlotThatMissesItsRollChance() {
        var shards = Table("WC-Shards", rollChance: 0.5);

        Assert.Null(Resolve(shards, [shards], 0.9));
    }

    [Fact]
    public void ResolveSlot_FallsBackWithinTheSlotsOwnHoard() {
        var skyvernWildcard = Table("SkyvernHoardLT-Wildcard");
        var ninjaWildcard = Table("NinjaLorePackLT-Wildcard");
        var ninjaRare = Table("NinjaLorePackLT-Rare");
        var skyvernRare = Table("SkyvernHoardLT-Rare");
        var skyvernUncommon = Table("SkyvernHoardLT-UnCommon");
        var ninjaUncommon = Table("NinjaLorePackLT-UnCommon");
        IReadOnlyList<DropTable> set84 =
            [skyvernWildcard, ninjaWildcard, ninjaRare, skyvernRare, skyvernUncommon, ninjaUncommon, Table("NinjaLorePackLT-UnCommon")];

        Assert.Same(skyvernRare, Resolve(skyvernWildcard, set84, 0.5, 0.01));
        Assert.Same(ninjaRare, Resolve(ninjaWildcard, set84, 0.5, 0.01));
        Assert.Same(skyvernUncommon, Resolve(skyvernRare, set84, 0.5));
        Assert.Same(ninjaUncommon, Resolve(ninjaRare, set84, 0.5));
    }

    [Fact]
    public void ResolveSlot_WrapsToAnEarlierTableOfTheSameHoard() {
        var uncommon = Table("ImmortalsLorePackLT-Uncommon");
        var rare = Table("ImmortalsLorePackLT-Rare");

        Assert.Same(uncommon, Resolve(rare, [uncommon, Table("NinjaLorePackLT-Uncommon"), rare], 0.5));
    }

    [Fact]
    public void ResolveSlot_UsesTheTableChanceWhenTheDataSetsOne() {
        var uncommon = Table("Set-Uncommon", rollChance: 0.98, noneChance: 0.02);

        Assert.Same(uncommon, Resolve(uncommon, [uncommon, Table("Set-Common")], 0.5));
    }

    [Fact]
    public void ResolveSlot_IgnoresPityWhenTheDataLeavesItAtZero() {
        var pity = new PackPity();
        for (var i = 0; i < 10; i++) {
            Assert.Same(s_common, PackSlotOdds.ResolveSlot(0, [s_rare, s_common], PackTierChances.Default,
                new FixedRandom(0.99, 0.99), pity));
        }
    }

    [Fact]
    public void ResolveSlot_GrantsRareAfterThePityCountOfFailedGates() {
        var rare = Table("Set-Rare", pityCounter: 2);
        var pity = new PackPity();
        var common = Table("Set-Common");
        IReadOnlyList<DropTable> pack = [rare, common];

        var resolved = Enumerable.Range(0, 4)
            .Select(_ => PackSlotOdds.ResolveSlot(0, pack, PackTierChances.Default, new FixedRandom(0.99, 0.99), pity))
            .ToList();

        Assert.Equal([common, common, rare, common], resolved);
    }

    [Fact]
    public void PickWeighted_FollowsTheItemWeights() {
        List<DropItem> items = [new() { ItemId = "1" }, new() { ItemId = "2", Weight = 3 }];
        var random = new Random(52);

        var heavyPicks = Enumerable.Range(0, 10_000).Count(_ => DropTableRoller.PickWeighted(items, random) == 1);

        Assert.InRange(heavyPicks, 7_300, 7_700);
    }

    [Fact]
    public void PickWeighted_IsUniformWithoutWeights() {
        List<DropItem> items = [new() { ItemId = "1" }, new() { ItemId = "2" }];
        var random = new Random(52);

        var secondPicks = Enumerable.Range(0, 10_000).Count(_ => DropTableRoller.PickWeighted(items, random) == 1);

        Assert.InRange(secondPicks, 4_800, 5_200);
    }

    private static DropTable? Resolve(DropTable slot, IReadOnlyList<DropTable> pack, params double[] rolls)
        => PackSlotOdds.ResolveSlot(pack.ToList().IndexOf(slot), pack, PackTierChances.Default, new FixedRandom(rolls),
            new PackPity());

    [Fact]
    public void PickWeighted_IsUniformWhenNoItemHasAPositiveWeight() {
        List<DropItem> items = [new() { ItemId = "1", Weight = 0 }, new() { ItemId = "2", Weight = -1 }];
        var random = new Random(52);

        var firstPicks = Enumerable.Range(0, 10_000).Count(_ => DropTableRoller.PickWeighted(items, random) == 0);

        Assert.InRange(firstPicks, 4_800, 5_200);
    }

    private static DropTable Table(string name, double rollChance = 1.0, double noneChance = 0.0, double pityCounter = 0.0)
        => new() { Name = name, RollChance = rollChance, NoneChance = noneChance, PityCounter = pityCounter };

    private sealed class FixedRandom(params double[] rolls) : Random {

        private readonly Queue<double> _rolls = new(rolls);

        public override double NextDouble() => _rolls.Dequeue();

    }

}
