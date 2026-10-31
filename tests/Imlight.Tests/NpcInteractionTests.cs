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
 * Last Updated: 10/27/2026
 */

using System;
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
    public void OptionTable_Build_SetsEchoedIndexToFlatPosition() {
        var quest = new FakeService();
        var vendor = new FakeService();
        var table = new ServiceOptionTable();
        var questOption = new EquipmentShopOption();
        var vendorOption = new EquipmentShopOption();

        table.Build(FirstPlayer, [(quest, [questOption]), (vendor, [vendorOption])]);

        Assert.Equal(0u, questOption.m_serviceIndex);
        Assert.Equal(1u, vendorOption.m_serviceIndex);
        Assert.True(table.TryRoute(FirstPlayer, vendorOption.m_serviceIndex, out var owner, out var localIndex));
        Assert.Same(vendor, owner);
        Assert.Equal(0u, localIndex);
    }

    [Fact]
    public void OptionTable_PlayerLeave_Evicts() {
        var table = new ServiceOptionTable();
        table.Build(FirstPlayer, [(new FakeService(), [new EquipmentShopOption()])]);

        table.Remove(FirstPlayer);

        Assert.False(table.TryRoute(FirstPlayer, 0, out _, out _));
    }

    [Fact]
    public void OptionTable_FindServiceIndex_ReturnsOwnersFirstFlatIndex() {
        var vendor = new FakeService();
        var offer = new FakeService();
        var table = new ServiceOptionTable();
        table.Build(FirstPlayer, [(vendor, [new EquipmentShopOption()]), (offer, [new EquipmentShopOption(), new EquipmentShopOption()])]);

        Assert.True(table.TryFindServiceIndex(FirstPlayer, offer, out var index));
        Assert.Equal(1u, index);
        Assert.False(table.TryFindServiceIndex(FirstPlayer, new FakeService(), out _));
    }

    [Fact]
    public void Refresh_VendorOnlyNpc_IsSkipped() {
        var vendor = new InteractVendorComponent(null!);

        Assert.False(InteractServiceMementoComponent.RefreshesOnQuestChange([vendor]));
        Assert.True(InteractServiceMementoComponent.RefreshesOnQuestChange([vendor, new InteractQuestOfferComponent(null!)]));
    }

    [Fact]
    public void Refresh_SuffixedPersonaName_MatchesNpc() {
        Assert.True(InteractServiceMementoComponent.IsPersona("WC-ST01-NPC03", "WC-ST01-NPC03_Persona"));
    }

    [Fact]
    public void Refresh_OtherPersonaName_DoesNotMatchNpc() {
        Assert.False(InteractServiceMementoComponent.IsPersona("WC-ST01-NPC03", "WC-ST01-NPC04_Persona"));
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

        var shown = InteractQuestOfferComponent.FindAvailableQuests([beta, alpha], wizard, QuestOfferGate.Open);
        var options = table.Build(FirstPlayer, [(offer, [.. shown.Select(offer.ToOfferOption)])]);

        Assert.Equal(["Title_Alpha", "Title_Beta"], options.Select(o => (string) ((PrepEntry) o).m_displayKey));
        Assert.Same(alpha, ClickOffer(table, offer, 0, [beta, alpha], wizard));
        Assert.Same(beta, ClickOffer(table, offer, 1, [beta, alpha], wizard));
    }

    [Fact]
    public void QuestOffer_SharedTitle_ClickResolvesByIdentity() {
        var first = new QuestTemplate { m_questName = "Alpha", m_questTitle = "To Ravenwood!" };
        var second = new QuestTemplate { m_questName = "Beta", m_questTitle = "To Ravenwood!" };
        var offer = new InteractQuestOfferComponent(null!);
        var table = new ServiceOptionTable();
        var wizard = NewWizard();
        var shown = InteractQuestOfferComponent.FindAvailableQuests([first, second], wizard, QuestOfferGate.Open);
        table.Build(FirstPlayer, [(offer, [.. shown.Select(offer.ToOfferOption)])]);

        Assert.Same(first, ClickOffer(table, offer, 0, [first, second], wizard));
        Assert.Same(second, ClickOffer(table, offer, 1, [first, second], wizard));
    }

    [Fact]
    public void QuestOffer_SharedTitleOldStyleAndCurrent_FlagsStaleTwin() {
        var old = new QuestTemplate { m_questName = "WC-ST01-C02-Old", m_questTitle = "Same" };
        var current = new QuestTemplate { m_questName = "WC-Current", m_questTitle = "Same" };
        var otherOld = new QuestTemplate { m_questName = "WC-ST01-C03-Old", m_questTitle = "Same" };

        Assert.True(InteractQuestOfferComponent.HasStaleTitleTwin([old, current]));
        Assert.False(InteractQuestOfferComponent.HasStaleTitleTwin([old, otherOld]));
        Assert.False(InteractQuestOfferComponent.HasStaleTitleTwin([current]));
    }

    [Fact]
    public void QuestOffer_HeldQuest_NotOffered() {
        var held = new QuestTemplate { m_questName = "Held" };
        var open = new QuestTemplate { m_questName = "Open" };

        var quests = InteractQuestOfferComponent.FindAvailableQuests([held, open], NewWizard("Held"), QuestOfferGate.Open);

        Assert.Equal([open], quests);
    }

    [Fact]
    public void QuestOffer_QuestTakenAfterOptionBuild_ClickDoesNotOfferAnother() {
        var alpha = new QuestTemplate { m_questName = "Alpha", m_questTitle = "Title_Alpha" };
        var beta = new QuestTemplate { m_questName = "Beta", m_questTitle = "Title_Beta" };
        var offer = new InteractQuestOfferComponent(null!);
        var table = new ServiceOptionTable();
        var wizard = NewWizard();
        var shown = InteractQuestOfferComponent.FindAvailableQuests([alpha, beta], wizard, QuestOfferGate.Open);
        table.Build(FirstPlayer, [(offer, [.. shown.Select(offer.ToOfferOption)])]);

        wizard.QuestBehavior.CurrentQuestInstances.Add(new QuestInstance { QuestName = "Alpha" });

        Assert.Null(ClickOffer(table, offer, 0, [alpha, beta], wizard));
        Assert.Same(beta, ClickOffer(table, offer, 1, [alpha, beta], wizard));
    }

    [Theory]
    [InlineData(-1, false, true)]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(1, true, false)]
    public void QuestOffer_CompletedQuest_OfferedAgainOnlyWhenRepeatable(int questRepeat, bool prepAlways, bool offered) {
        var quest = new QuestTemplate { m_questName = "Weekly", m_questRepeat = questRepeat, m_prepAlways = prepAlways };
        var wizard = NewWizard();
        wizard.QuestBehavior.AddToQuestRegistry("Weekly", "Complete", 1);

        var quests = InteractQuestOfferComponent.FindAvailableQuests([quest], wizard, QuestOfferGate.Open);

        Assert.Equal(offered, quests.Contains(quest));
    }

    [Fact]
    public void QuestOffer_HeldRepeatableQuest_NotOffered() {
        var quest = new QuestTemplate { m_questName = "Weekly", m_questRepeat = -1 };

        Assert.Empty(InteractQuestOfferComponent.FindAvailableQuests([quest], NewWizard("Weekly"), QuestOfferGate.Open));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void QuestOffer_HiddenOrOutdated_NotOffered(bool hidden, bool outdated) {
        var quest = new QuestTemplate { m_questName = "Old", m_isHidden = hidden, m_outdated = outdated };

        Assert.Empty(InteractQuestOfferComponent.FindAvailableQuests([quest], NewWizard(), QuestOfferGate.Open));
    }

    [Fact]
    public void QuestUnderway_QuestWithoutUnderwayLines_HasNoUnderwayDialog() {
        var noTag = new QuestTemplate { m_dialogList = new ActorDialogList { m_dialogs = [new ActorDialog { m_dialogTag = "Prep" }] } };
        var emptyTag = new QuestTemplate { m_dialogList = new ActorDialogList { m_dialogs = [new ActorDialog { m_dialogTag = "Underway", m_dialogEntries = [] }] } };
        var withLines = new QuestTemplate {
            m_dialogList = new ActorDialogList {
                m_dialogs = [new ActorDialog { m_dialogTag = "Underway", m_dialogEntries = [new NPCDialogEntry { m_dialog = "QuestDialog_1" }] }]
            }
        };

        Assert.False(InteractQuestUnderwayComponent.HasUnderwayDialog(new QuestTemplate()));
        Assert.False(InteractQuestUnderwayComponent.HasUnderwayDialog(noTag));
        Assert.False(InteractQuestUnderwayComponent.HasUnderwayDialog(emptyTag));
        Assert.True(InteractQuestUnderwayComponent.HasUnderwayDialog(withLines));
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

    [Fact]
    public void Refresh_AcceptingOnlyOfferedQuest_OfferReplacedByPersonaGoal() {
        var alpha = new QuestTemplate { m_questName = "Alpha", m_questTitle = "Title_Alpha" };
        var offerComponent = new InteractQuestOfferComponent(null!);
        var offer = new CachingService(wizard
            => [.. InteractQuestOfferComponent.FindAvailableQuests([alpha], wizard, QuestOfferGate.Open).Select(offerComponent.ToOfferOption)]);
        var persona = new CachingService(wizard
            => wizard.HasQuest("Alpha") ? [new GoalEntry { m_displayKey = "Title_Alpha" }] : []);
        var table = new ServiceOptionTable();
        var wizard = NewWizard();
        table.Build(FirstPlayer, InteractServiceMementoComponent.CollectFreshOptions([offer, persona], wizard));

        wizard.QuestBehavior.CurrentQuestInstances.Add(new QuestInstance { QuestName = "Alpha" });
        var options = table.Build(FirstPlayer, InteractServiceMementoComponent.CollectFreshOptions([offer, persona], wizard));

        Assert.IsType<GoalEntry>(Assert.Single(options));
        Assert.True(table.TryRoute(FirstPlayer, 0, out var owner, out _));
        Assert.Same(persona, owner);
    }

    private static QuestTemplate ClickOffer(ServiceOptionTable table, InteractQuestOfferComponent offer, uint index,
                                            QuestTemplate[] npcQuests, Wizard wizard) {
        Assert.True(table.TryRoute(FirstPlayer, index, out var owner, out var localIndex));
        Assert.Same(offer, owner);
        Assert.True(table.TryGetSentOption(FirstPlayer, owner, localIndex, out var sent));

        return offer.FindClickedQuest(sent, InteractQuestOfferComponent.FindAvailableQuests(npcQuests, wizard, QuestOfferGate.Open));
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

    private sealed class CachingService(Func<Wizard, List<ServiceOptionBase>> optionsFor) : IServiceComponent {

        private readonly Dictionary<ulong, List<ServiceOptionBase>> _cachedOptions = [];

        public string ServiceName => "";
        public string NpcIcon => "";
        public string NpcNameKey => "";
        public string NpcTextKey => "";
        public WizBangs WizBang => default;
        public string StateName => "";
        public string InteractWizBang => "";
        public string DisplayKey => "";

        public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) {
            if (!_cachedOptions.TryGetValue(playerCharacter.CharId, out var options)) {
                options = optionsFor(playerCharacter);
                _cachedOptions[playerCharacter.CharId] = options;
            }

            return options;
        }

        public void ForgetPlayerState(Wizard playerCharacter)
            => _cachedOptions.Remove(playerCharacter.CharId);

        public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) { }

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


    [Fact]
    public void WizBang_IsSentTwice_ThenOnlyOnChange() {
        var log = new WizBangSendLog();
        var player = new object();

        Assert.True(log.ShouldSend(player, WizBangs.None));
        Assert.True(log.ShouldSend(player, WizBangs.None));
        Assert.False(log.ShouldSend(player, WizBangs.None));
        Assert.False(log.ShouldSend(player, WizBangs.None));

        Assert.True(log.ShouldSend(player, WizBangs.Shopping));
        Assert.True(log.ShouldSend(player, WizBangs.Shopping));
        Assert.False(log.ShouldSend(player, WizBangs.Shopping));
    }

    [Fact]
    public void WizBang_StartsOver_ForAPlayerWhoLeft() {
        var log = new WizBangSendLog();
        var player = new object();
        log.ShouldSend(player, WizBangs.Shopping);
        log.ShouldSend(player, WizBangs.Shopping);
        Assert.False(log.ShouldSend(player, WizBangs.Shopping));

        log.Forget(player);

        Assert.True(log.ShouldSend(player, WizBangs.Shopping));
    }

    [Fact]
    public void WizBang_IsTrackedPerPlayer() {
        var log = new WizBangSendLog();
        var first = new object();
        var second = new object();
        log.ShouldSend(first, WizBangs.None);
        log.ShouldSend(first, WizBangs.None);

        Assert.False(log.ShouldSend(first, WizBangs.None));
        Assert.True(log.ShouldSend(second, WizBangs.None));
    }

}
