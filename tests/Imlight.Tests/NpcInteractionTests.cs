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
 * NPC INTERACTION TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for NPC service options: a click resolves against the
 * option list that player was sent, and a vendor without inventory offers no shop.
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
 * Last Updated: 10/25/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class NpcInteractionTests {

    private const ulong FirstPlayer = 1;
    private const ulong SecondPlayer = 2;

    [Fact]
    public void OptionTable_PerPlayer_IndexRoutesToOwnComponent() {
        var quest = new FakeService();
        var vendor = new FakeService();
        var table = new ServiceOptionTable();

        table.Build(FirstPlayer, [(quest, [new EquipmentShopOption()]), (vendor, [new EquipmentShopOption()])]);
        table.Build(SecondPlayer, [(quest, []), (vendor, [new EquipmentShopOption()])]);

        Assert.True(table.TryRoute(FirstPlayer, 0, out var firstOwner, out _));
        Assert.Same(quest, firstOwner);
        Assert.True(table.TryRoute(SecondPlayer, 0, out var secondOwner, out var secondLocalIndex));
        Assert.Same(vendor, secondOwner);
        Assert.Equal(0u, secondLocalIndex);
    }

    [Fact]
    public void OptionTable_PlayerLeave_Evicts() {
        var table = new ServiceOptionTable();
        table.Build(FirstPlayer, [(new FakeService(), [new EquipmentShopOption()])]);

        table.Remove(FirstPlayer);

        Assert.False(table.TryRoute(FirstPlayer, 0, out _, out _));
    }

    [Fact]
    public void Vendor_NoInventory_OffersNoShopOption() {
        var vendor = new InteractVendorComponent(null!);

        Assert.Empty(vendor.GetServiceOptions(null!));
    }

    [Fact]
    public void Vendor_NullInventory_SendShopOfferings_DoesNotThrow() {
        var vendor = new InteractVendorComponent(null!);

        vendor.OnServiceInteraction(null!, null!, null!, 0);
        Assert.False(vendor.HasItem(new GID(1)));
    }

    private sealed class FakeService : IServiceComponent {

        public string ServiceName => "";
        public string NpcIcon => "";
        public string NpcNameKey => "";
        public string NpcTextKey => "";
        public WizBangs WizBang => default;
        public string StateName => "";
        public string InteractWizBang => "";
        public string DisplayKey => "";

        public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) => [];

        public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) { }

    }

}
