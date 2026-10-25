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
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Behaviors;
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

    [Fact]
    public void PersonaName_SuffixedName_MatchesObjectName() {
        Assert.Equal("WC-ST01-NPC03", PersonaName.Normalize("WC-ST01-NPC03_Persona"));
        Assert.Equal(PersonaName.Normalize("WC-ST01-NPC03"), PersonaName.Normalize("WC-ST01-NPC03_Persona"));
    }

    [Fact]
    public void PersonaName_OtherNames_Unchanged() {
        Assert.Equal("WC-ST01-NPC03", PersonaName.Normalize("WC-ST01-NPC03"));
        Assert.Equal("WC-ST01-NPC03_PersonaX", PersonaName.Normalize("WC-ST01-NPC03_PersonaX"));
    }

    [Fact]
    public void QuestOffer_TwoAvailableQuests_TwoOptionsRouteToEachQuest() {
        var beta = new QuestTemplate { m_questName = "Beta", m_questTitle = "Title_Beta" };
        var alpha = new QuestTemplate { m_questName = "Alpha", m_questTitle = "Title_Alpha" };
        var offer = new InteractQuestOfferComponent(null!);
        var table = new ServiceOptionTable();
        var wizard = NewWizard();

        var shown = InteractQuestOfferComponent.FindAvailableQuests([beta, alpha], wizard);
        var options = table.Build(FirstPlayer, [(offer, [.. shown.Select(offer.ToOfferOption)])]);

        Assert.Equal(["Title_Alpha", "Title_Beta"], options.Select(o => (string) ((PrepEntry) o).m_displayKey));
        Assert.Same(alpha, ClickOffer(table, offer, 0, [beta, alpha], wizard));
        Assert.Same(beta, ClickOffer(table, offer, 1, [beta, alpha], wizard));
    }

    [Fact]
    public void QuestOffer_HeldQuest_NotOffered() {
        var held = new QuestTemplate { m_questName = "Held" };
        var open = new QuestTemplate { m_questName = "Open" };

        var quests = InteractQuestOfferComponent.FindAvailableQuests([held, open], NewWizard("Held"));

        Assert.Equal([open], quests);
    }

    [Fact]
    public void QuestOffer_QuestTakenAfterOptionBuild_ClickDoesNotOfferAnother() {
        var alpha = new QuestTemplate { m_questName = "Alpha", m_questTitle = "Title_Alpha" };
        var beta = new QuestTemplate { m_questName = "Beta", m_questTitle = "Title_Beta" };
        var offer = new InteractQuestOfferComponent(null!);
        var table = new ServiceOptionTable();
        var wizard = NewWizard();
        var shown = InteractQuestOfferComponent.FindAvailableQuests([alpha, beta], wizard);
        table.Build(FirstPlayer, [(offer, [.. shown.Select(offer.ToOfferOption)])]);

        wizard.QuestBehavior.CurrentQuestInstances.Add(new QuestInstance { QuestName = "Alpha" });

        Assert.Null(ClickOffer(table, offer, 0, [alpha, beta], wizard));
        Assert.Same(beta, ClickOffer(table, offer, 1, [alpha, beta], wizard));
    }

    [Fact]
    public void QuestUnderway_TwoHeldQuests_TwoOptionsRouteToEachQuest() {
        var beta = new QuestTemplate { m_questName = "Beta", m_questTitle = "Title_Beta" };
        var alpha = new QuestTemplate { m_questName = "Alpha", m_questTitle = "Title_Alpha" };
        var other = new QuestTemplate { m_questName = "NotHeld" };
        var underway = new InteractQuestUnderwayComponent(null!);
        var table = new ServiceOptionTable();
        var wizard = NewWizard("Alpha", "Beta");

        var shown = InteractQuestUnderwayComponent.FindUnderwayQuests([beta, other, alpha], wizard);
        var options = table.Build(FirstPlayer, [(underway, [.. shown.Select(underway.ToUnderwayOption)])]);

        Assert.Equal(["Title_Alpha", "Title_Beta"], options.Select(o => (string) ((PrepEntry) o).m_displayKey));
        Assert.Same(alpha, ClickUnderway(table, underway, 0, [beta, other, alpha], wizard));
        Assert.Same(beta, ClickUnderway(table, underway, 1, [beta, other, alpha], wizard));
    }

    [Fact]
    public void QuestUnderway_QuestDroppedAfterOptionBuild_ClickShowsNoOther() {
        var alpha = new QuestTemplate { m_questName = "Alpha", m_questTitle = "Title_Alpha" };
        var beta = new QuestTemplate { m_questName = "Beta", m_questTitle = "Title_Beta" };
        var underway = new InteractQuestUnderwayComponent(null!);
        var table = new ServiceOptionTable();
        var wizard = NewWizard("Alpha", "Beta");
        var shown = InteractQuestUnderwayComponent.FindUnderwayQuests([alpha, beta], wizard);
        table.Build(FirstPlayer, [(underway, [.. shown.Select(underway.ToUnderwayOption)])]);

        wizard.QuestBehavior.CurrentQuestInstances.RemoveAll(q => q.QuestName == "Alpha");

        Assert.Null(ClickUnderway(table, underway, 0, [alpha, beta], wizard));
    }

    private static QuestTemplate ClickOffer(ServiceOptionTable table, InteractQuestOfferComponent offer, uint index,
                                            QuestTemplate[] npcQuests, Wizard wizard) {
        Assert.True(table.TryRoute(FirstPlayer, index, out var owner, out var localIndex));
        Assert.Same(offer, owner);
        Assert.True(table.TryGetSentOption(FirstPlayer, owner, localIndex, out var sent));

        return InteractQuestOfferComponent.FindClickedQuest(sent, InteractQuestOfferComponent.FindAvailableQuests(npcQuests, wizard));
    }

    private static QuestTemplate ClickUnderway(ServiceOptionTable table, InteractQuestUnderwayComponent underway, uint index,
                                               QuestTemplate[] npcQuests, Wizard wizard) {
        Assert.True(table.TryRoute(FirstPlayer, index, out var owner, out var localIndex));
        Assert.Same(underway, owner);
        Assert.True(table.TryGetSentOption(FirstPlayer, owner, localIndex, out var sent));

        return InteractQuestUnderwayComponent.FindClickedQuest(sent, InteractQuestUnderwayComponent.FindUnderwayQuests(npcQuests, wizard));
    }

    private static Wizard NewWizard(params string[] heldQuests) {
        var questBehavior = new ServerQuestBehavior();
        foreach (var questName in heldQuests) {
            questBehavior.CurrentQuestInstances.Add(new QuestInstance { QuestName = questName });
        }

        return new Wizard { QuestBehavior = questBehavior };
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
