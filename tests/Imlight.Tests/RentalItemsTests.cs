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
 * RENTAL ITEMS TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the one-rental-per-type rule that the shop, the crown shop and gift redemption share.
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
 * Last Updated: 10/31/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Game.Rentals;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using Xunit;

namespace Imlight.Tests;

public sealed class RentalItemsTests {

    private const ulong MountRentalId = 917_001;
    private const ulong OtherMountRentalId = 917_002;
    private const ulong PetRentalId = 917_003;
    private const ulong PlainItemId = 917_004;
    private const ulong GoldId = 917_005;
    private const ulong LunariId = 917_006;
    private const ulong TokensId = 917_007;
    private const ulong SpellId = 917_008;
    private const ulong NoTemplateId = 917_009;
    private const ulong PackId = 917_010;
    private const ulong ReagentId = 917_011;
    private const ulong SnackId = 917_012;
    private const ulong InnerBundleId = 917_013;
    private const ulong AnyGiftId = 917_099;

    private static readonly WizItemTemplate s_mountRental = Rental("Mount");
    private static readonly WizItemTemplate s_otherMountRental = Rental("Mount");
    private static readonly WizItemTemplate s_petRental = Rental("Pet");
    private static readonly WizItemTemplate s_plainItem = new() { m_behaviors = [] };

    public RentalItemsTests() {
        var cache = (ConcurrentDictionary<ulong, CoreTemplate>) typeof(CoreObjectFactory)
            .GetField("s_templateCache", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        cache[MountRentalId] = s_mountRental;
        cache[OtherMountRentalId] = s_otherMountRental;
        cache[PetRentalId] = s_petRental;
        cache[PlainItemId] = s_plainItem;
        cache[GoldId] = new GoldAmountTemplate { m_behaviors = [] };
        cache[LunariId] = new LunariAmountTemplate { m_behaviors = [] };
        cache[TokensId] = new TourneyTokensAmountTemplate { m_behaviors = [] };
        cache[SpellId] = new SpellTemplate();
        cache[PackId] = new BoosterPackTemplate();
        cache[ReagentId] = new ReagentItemTemplate();
        cache[SnackId] = new PetSnackItemTemplate();
        cache[InnerBundleId] = BundleOf(PlainItemId);
    }

    [Fact]
    public void WouldDuplicateType_SameTypeInBackpack_IsTrue() {
        Assert.True(RentalItems.WouldDuplicateType(Holding(backpack: MountRentalId), s_otherMountRental));
    }

    [Fact]
    public void WouldDuplicateType_SameTypeWorn_IsTrue() {
        Assert.True(RentalItems.WouldDuplicateType(Holding(worn: MountRentalId), s_otherMountRental));
    }

    [Fact]
    public void WouldDuplicateType_OtherTypeHeld_IsFalse() {
        Assert.False(RentalItems.WouldDuplicateType(Holding(backpack: PetRentalId), s_mountRental));
    }

    [Fact]
    public void WouldDuplicateType_NotARental_IsFalse() {
        Assert.False(RentalItems.WouldDuplicateType(Holding(backpack: MountRentalId), s_plainItem));
    }

    [Fact]
    public void WouldDuplicateType_TwoCopiesAtOnce_IsTrue() {
        Assert.True(RentalItems.WouldDuplicateType(Holding(), s_mountRental, count: 2));
        Assert.False(RentalItems.WouldDuplicateType(Holding(), s_plainItem, count: 2));
    }

    [Fact]
    public void WouldDuplicateType_BundleCountsAsItsContents() {
        var mountAndPet = new ItemBundleTemplate { m_bundleItems = [(uint) MountRentalId, (uint) PetRentalId] };
        var twoMounts = new ItemBundleTemplate { m_bundleItems = [(uint) MountRentalId, (uint) OtherMountRentalId] };

        Assert.False(RentalItems.WouldDuplicateType(Holding(), mountAndPet));
        Assert.True(RentalItems.WouldDuplicateType(Holding(backpack: PetRentalId), mountAndPet));
        Assert.True(RentalItems.WouldDuplicateType(Holding(), twoMounts));
    }

    [Fact]
    public void CrownShopCharge_DuplicateRental_RefusesAndChargesNothing() {
        var charges = 0;

        var error = CrownShopPurchaseRules.ChargeUnlessDuplicateRental(Holding(backpack: MountRentalId), s_otherMountRental, 1, () => {
            charges++;
            return 0;
        });

        Assert.Equal(CrownShopErrors.PriceLockMismatch, error);
        Assert.Equal(0, charges);
    }

    [Fact]
    public void CrownShopCharge_NoDuplicate_ReturnsTheDebitResult() {
        const int NotEnoughCrowns = 9;

        Assert.Equal(NotEnoughCrowns, CrownShopPurchaseRules.ChargeUnlessDuplicateRental(Holding(), s_mountRental, 1, () => NotEnoughCrowns));
    }

    [Fact]
    public void DuplicatesItself_ContentsSharingAType_IsTrue() {
        Assert.True(RentalItems.DuplicatesItself(s_mountRental, 2));
        Assert.True(RentalItems.DuplicatesItself(new ItemBundleTemplate { m_bundleItems = [(uint) MountRentalId, (uint) OtherMountRentalId] }, 1));
    }

    [Fact]
    public void DuplicatesItself_NothingShared_IsFalse() {
        Assert.False(RentalItems.DuplicatesItself(s_mountRental, 1));
        Assert.False(RentalItems.DuplicatesItself(s_plainItem, 2));
        Assert.False(RentalItems.DuplicatesItself(new ItemBundleTemplate { m_bundleItems = [(uint) MountRentalId, (uint) PetRentalId] }, 1));
    }

    [Theory]
    [MemberData(nameof(GiftsThatYieldADudOrNothing))]
    public void GiftCharge_DudOrNothing_RefusesAndChargesNothing(CoreTemplate template, ulong templateId, int count) {
        var charges = 0;

        var error = CrownShopPurchaseRules.ChargeGiftUnlessUnredeemable(Sender(), template, templateId, count, () => {
            charges++;
            return 0;
        });

        Assert.Equal(CrownShopErrors.PriceLockMismatch, error);
        Assert.Equal(0, charges);
    }

    public static TheoryData<CoreTemplate, ulong, int> GiftsThatYieldADudOrNothing() => new() {
        { s_mountRental, MountRentalId, 2 },
        { BundleOf(MountRentalId, OtherMountRentalId), AnyGiftId, 1 },
        { new TourneyTokensAmountTemplate(), TokensId, 1 },
        { new SpellTemplate(), SpellId, 1 },
        { null, NoTemplateId, 1 },
        { BundleOf(PlainItemId, GoldId), AnyGiftId, 1 },
        { BundleOf(PlainItemId, LunariId), AnyGiftId, 1 },
        { BundleOf(PlainItemId, TokensId), AnyGiftId, 1 },
        { BundleOf(PlainItemId, SpellId), AnyGiftId, 1 },
        { BundleOf(PlainItemId, NoTemplateId), AnyGiftId, 1 },
        { BundleOf(PlainItemId, InnerBundleId), AnyGiftId, 1 },
        { BundleOf(), AnyGiftId, 1 },
    };

    [Theory]
    [MemberData(nameof(NormalGifts))]
    public void GiftCharge_NormalGift_ReturnsTheDebitResult(CoreTemplate template, ulong templateId, int count) {
        const int NotEnoughCrowns = 9;

        Assert.Equal(NotEnoughCrowns, CrownShopPurchaseRules.ChargeGiftUnlessUnredeemable(Sender(), template, templateId, count, () => NotEnoughCrowns));
    }

    public static TheoryData<CoreTemplate, ulong, int> NormalGifts() => new() {
        { s_plainItem, PlainItemId, 2 },
        { s_mountRental, MountRentalId, 1 },
        { new GoldAmountTemplate(), GoldId, 1 },
        { new LunariAmountTemplate(), LunariId, 1 },
        { new BoosterPackTemplate(), PackId, 1 },
        { new ReagentItemTemplate(), ReagentId, 1 },
        { new PetSnackItemTemplate(), SnackId, 1 },
        { BundleOf(PlainItemId, MountRentalId, PackId, ReagentId, SnackId), AnyGiftId, 1 },
    };

    [Theory]
    [MemberData(nameof(GiftsRedemptionStillClears))]
    public void Redemption_BundleThatYieldsADud_IsStillGranted(CoreTemplate bundle) {
        Assert.True(GiftingService.CanGrantGift(Sender(), bundle, AnyGiftId));
    }

    public static TheoryData<CoreTemplate> GiftsRedemptionStillClears() => new() {
        BundleOf(PlainItemId, GoldId),
        BundleOf(PlainItemId, LunariId),
        BundleOf(PlainItemId, TokensId),
        BundleOf(PlainItemId, InnerBundleId),
        BundleOf(),
    };

    [Theory]
    [MemberData(nameof(GiftsRedemptionAlwaysFails))]
    public void Redemption_NonItemOrTopLevelTokens_IsNeverGranted(CoreTemplate template, ulong templateId) {
        Assert.False(GiftingService.CanGrantGift(Sender(), template, templateId));
    }

    public static TheoryData<CoreTemplate, ulong> GiftsRedemptionAlwaysFails() => new() {
        { new TourneyTokensAmountTemplate(), TokensId },
        { new SpellTemplate(), SpellId },
        { null, NoTemplateId },
        { BundleOf(PlainItemId, SpellId), AnyGiftId },
        { BundleOf(PlainItemId, NoTemplateId), AnyGiftId },
    };

    [Fact]
    public void PackSlot_RollDuplicatesOwnedRental_IsRerolled() {
        var rolls = RollsOf(MountRentalId, PetRentalId);

        var kept = PackManager.RollSlotWithoutDuplicateRentals(rolls, Holding(backpack: OtherMountRentalId), []);

        Assert.Equal(PetRentalId.ToString(), Assert.Single(kept).ItemId);
    }

    [Fact]
    public void PackSlot_RollDuplicatesEarlierDrop_IsRerolled() {
        var rolls = RollsOf(MountRentalId, PlainItemId);

        var kept = PackManager.RollSlotWithoutDuplicateRentals(rolls, Holding(), [s_otherMountRental]);

        Assert.Equal(PlainItemId.ToString(), Assert.Single(kept).ItemId);
    }

    [Fact]
    public void PackSlot_OnlyDuplicatesToRoll_KeepsNothing() {
        var rolls = 0;

        var kept = PackManager.RollSlotWithoutDuplicateRentals(() => {
            rolls++;
            return [new DropItemResult { ItemId = MountRentalId.ToString() }];
        }, Holding(backpack: OtherMountRentalId), []);

        Assert.Empty(kept);
        Assert.InRange(rolls, 2, 100);
    }

    [Fact]
    public void PackSlot_NoRental_KeepsTheFirstRoll() {
        var rolls = 0;

        var kept = PackManager.RollSlotWithoutDuplicateRentals(() => {
            rolls++;
            return [new DropItemResult { ItemId = PlainItemId.ToString() }];
        }, Holding(backpack: MountRentalId), []);

        Assert.Single(kept);
        Assert.Equal(1, rolls);
    }

    private static Func<List<DropItemResult>> RollsOf(params ulong[] templateIds) {
        var next = new Queue<ulong>(templateIds);

        return () => [new DropItemResult { ItemId = next.Dequeue().ToString() }];
    }

    private static ItemBundleTemplate BundleOf(params ulong[] templateIds)
        => new() { m_behaviors = [], m_bundleItems = [.. templateIds.Select(id => (uint) id)] };

    private static Wizard Sender()
        => new() { PlayerNameBehavior = new ServerWizPlayerNameBehavior { NameOverride = "Sender" } };

    private static WizItemTemplate Rental(string type)
        => new() { m_behaviors = [new RentalBehaviorTemplate { m_typeList = [type] }] };

    private static Wizard Holding(ulong? backpack = null, ulong? worn = null)
        => new() {
            InventoryBehavior = new ServerWizInventoryBehavior { Items = ItemsOf(backpack) },
            EquipmentBehavior = new ServerWizEquipmentBehavior { EquippedItems = ItemsOf(worn) },
        };

    private static List<WizClientObjectItem> ItemsOf(ulong? templateId)
        => templateId is { } id ? [new WizClientObjectItem { m_templateID = new GID(id) }] : [];

}
