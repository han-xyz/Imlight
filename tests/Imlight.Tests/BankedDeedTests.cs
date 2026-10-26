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
 * BANKED DEED TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for deeds in the banks: a banked deed counts as held, and the shared bank refuses what housing
 * recognises as a house.
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

using Imcodec.ObjectProperty.TypeCache;
using System.Linq;
using System.Runtime.CompilerServices;
using Imcodec.Types;
using Imlight.CoreLib.Game.Bank;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class BankedDeedTests {

    private const uint DeedTemplateId = 0x7E57_0001;

    [Fact]
    public void HeldItems_CountDeedInPersonalBank() {
        var deed = new WizClientObjectItem { m_templateID = new GID(DeedTemplateId) };
        var wizard = Bare();
        wizard.StorageBehavior.Items.Add(deed);

        Assert.Contains(deed, HouseDeeds.HeldItems(wizard));
        Assert.Contains(deed, HouseDeeds.OwnItems(wizard));
    }

    [Fact]
    public void IsHouse_RecognisesHouseTemplate() {
        HouseTemplates.Register(new HouseTemplateInfo(DeedTemplateId, "Lot", 0, "", "", 0, 0));

        Assert.True(BankRules.IsHouse(new WizClientObjectItem { m_templateID = new GID(DeedTemplateId) }));
    }

    [Fact]
    public void HeldItems_CountOnlyOwnSharedBankItems() {
        var own = new WizClientObjectItem { m_templateID = new GID(DeedTemplateId), m_characterId = new GID(5) };
        var other = new WizClientObjectItem { m_templateID = new GID(DeedTemplateId), m_characterId = new GID(6) };
        var wizard = Bare();
        wizard.CharId = 5;
        var account = (Account) RuntimeHelpers.GetUninitializedObject(typeof(Account));
        account.SharedBank = new AccountSharedBank { Items = [own, other] };
        wizard.Account = account;

        Assert.Equal([own], HouseDeeds.HeldItems(wizard).ToList());
        Assert.Empty(HouseDeeds.OwnItems(wizard));
    }

    private static Wizard Bare()
        => new() { InventoryBehavior = new ServerWizInventoryBehavior { Items = [] }, StorageBehavior = new ServerWizStorageBehavior() };

}
