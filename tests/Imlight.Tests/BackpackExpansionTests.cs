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
 * BACKPACK EXPANSION TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the Expand Backpack Elixir: the extra space it adds widens the backpack, up to the limit.
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

using System.Text.Json;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Game.Elixirs;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class BackpackExpansionTests {

    private const ulong ElixirTemplateId = 1431039;
    private const ulong OtherElixirTemplateId = 1439023;
    private const int SlotsPerElixir = 25;

    [Fact]
    public void IsBackpackExpansion_MatchesOnlyTheExpandBackpackElixir() {
        Assert.True(BackpackExpansion.IsBackpackExpansion(ElixirTemplateId));
        Assert.False(BackpackExpansion.IsBackpackExpansion(OtherElixirTemplateId));
    }

    [Fact]
    public void ExtraSpace_WidensTheBackpackCap() {
        var wizard = Bare();
        var inventory = wizard.InventoryBehavior;
        var baseSlots = inventory.FreeSlots;
        Fill(wizard, baseSlots);
        Assert.False(inventory.HasInventorySpace(1));

        inventory.ExtraSpace = SlotsPerElixir;

        Assert.Equal(SlotsPerElixir, inventory.FreeSlots);
        Assert.True(inventory.HasInventorySpace(SlotsPerElixir));
        Assert.False(inventory.HasInventorySpace(SlotsPerElixir + 1));
        Assert.True(inventory.AddItem(Item(9_000)));
        Assert.Equal(baseSlots + SlotsPerElixir, inventory.GetClientBehaviorInstance().m_numItemsAllowed);
    }

    [Fact]
    public void ExtraSpace_IsStoredWithTheInventory() {
        var inventory = new ServerWizInventoryBehavior { Items = [], InventoryItemIds = [], ExtraSpace = SlotsPerElixir };

        var stored = JsonSerializer.Deserialize<ServerWizInventoryBehavior>(JsonSerializer.Serialize(inventory));

        Assert.Equal(SlotsPerElixir, stored!.ExtraSpace);
    }

    [Fact]
    public void TryUse_ConsumesTheElixirAndAddsTheIncrementOnce() {
        var wizard = Bare();
        var elixir = Item(1);
        wizard.InventoryBehavior.AddItem(elixir);
        var freeBefore = wizard.InventoryBehavior.FreeSlots;

        Assert.True(BackpackExpansion.TryUse(wizard, elixir));

        Assert.False(wizard.InventoryBehavior.HasItem(elixir.m_globalID));
        Assert.Equal(SlotsPerElixir, wizard.InventoryBehavior.ExtraSpace);
        Assert.Equal(freeBefore + 1 + SlotsPerElixir, wizard.InventoryBehavior.FreeSlots);
    }

    [Fact]
    public void TryUse_AtTheLimitKeepsTheElixir() {
        var wizard = Bare();
        var elixirs = new[] { Item(1), Item(2), Item(3) };
        foreach (var elixir in elixirs) {
            wizard.InventoryBehavior.AddItem(elixir);
        }

        Assert.True(BackpackExpansion.TryUse(wizard, elixirs[0]));
        Assert.True(BackpackExpansion.TryUse(wizard, elixirs[1]));
        Assert.False(BackpackExpansion.TryUse(wizard, elixirs[2]));

        Assert.True(wizard.InventoryBehavior.HasItem(elixirs[2].m_globalID));
        Assert.Equal(2 * SlotsPerElixir, wizard.InventoryBehavior.ExtraSpace);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(0, 2, true)]
    [InlineData(0, 3, false)]
    [InlineData(SlotsPerElixir, 2, false)]
    [InlineData(2 * SlotsPerElixir, 1, false)]
    public void MayPurchase_RefusesPastTheLimit(int extraSpace, int count, bool expected) {
        var wizard = Bare();
        wizard.InventoryBehavior.ExtraSpace = extraSpace;

        Assert.Equal(expected, BackpackExpansion.MayPurchase(wizard, count));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(SlotsPerElixir, false)]
    [InlineData(2 * SlotsPerElixir, true)]
    public void Requirements_FollowTheLimit(int extraSpace, bool atLimit) {
        var wizard = Bare();
        wizard.InventoryBehavior.ExtraSpace = extraSpace;

        Assert.Equal(atLimit, Evaluate(new ReqIsLimitExpandBackpackElixirs(), wizard));
        Assert.Equal(!atLimit, Evaluate(new ReqAddIncreaseInventory(), wizard));
    }

    private static bool Evaluate(Requirement requirement, Wizard wizard) {
        var requirements = new RequirementList { m_requirements = [requirement] };

        return RequirementDispatcher.EvaluateRequirements(requirements, new GenericRequirementContext(requirements, null, null, wizard));
    }

    private static void Fill(Wizard wizard, int count) {
        for (var i = 0; i < count; i++) {
            Assert.True(wizard.InventoryBehavior.AddItem(Item(100_000 + (ulong) i)));
        }
    }

    private static WizClientObjectItem Item(ulong globalId)
        => new() { m_globalID = new GID(globalId), m_templateID = new GID(ElixirTemplateId) };

    private static Wizard Bare()
        => new() {
            GameStats = new ServerWizGameStats(MagicSchool.Fire, 1),
            InventoryBehavior = new ServerWizInventoryBehavior { Items = [], InventoryItemIds = [] }
        };

}
