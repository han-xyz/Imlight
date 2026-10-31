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
using System.Reflection;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Game.Rentals;
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
    [MemberData(nameof(NeverRedeemableGifts))]
    public void GiftCharge_NeverRedeemable_RefusesAndChargesNothing(CoreTemplate template, int count) {
        var charges = 0;

        var error = CrownShopPurchaseRules.ChargeGiftUnlessUnredeemable(template, count, () => {
            charges++;
            return 0;
        });

        Assert.Equal(CrownShopErrors.PriceLockMismatch, error);
        Assert.Equal(0, charges);
    }

    public static TheoryData<CoreTemplate, int> NeverRedeemableGifts() => new() {
        { s_mountRental, 2 },
        { new ItemBundleTemplate { m_bundleItems = [(uint) MountRentalId, (uint) OtherMountRentalId] }, 1 },
        { new TourneyTokensAmountTemplate(), 1 },
    };

    [Fact]
    public void GiftCharge_RedeemableOnce_ReturnsTheDebitResult() {
        const int NotEnoughCrowns = 9;

        Assert.Equal(NotEnoughCrowns, CrownShopPurchaseRules.ChargeGiftUnlessUnredeemable(s_mountRental, 1, () => NotEnoughCrowns));
    }

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
