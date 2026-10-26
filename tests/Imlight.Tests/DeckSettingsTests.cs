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
 * DECK SETTINGS TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for naming a deck and choosing its archmastery school: only on decks the wizard owns.
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

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class DeckSettingsTests {

    private const int LockedPattern = unchecked((int) 0x80000000);

    private static WizClientObjectItem Deck(ulong id)
        => new() { m_globalID = id, m_pattern = LockedPattern, m_inactiveBehaviors = [new DeckBehavior()] };

    private static Wizard WizardWith(WizClientObjectItem inventoryItem, WizClientObjectItem equippedItem) => new() {
        CharId = 1,
        InventoryBehavior = new ServerWizInventoryBehavior { Items = [inventoryItem] },
        EquipmentBehavior = new ServerWizEquipmentBehavior { EquippedItems = [equippedItem] },
    };

    [Fact]
    public void NamingAnOwnedDeckKeepsItsLock() {
        var deck = Deck(10);
        var wizard = WizardWith(deck, Deck(11));

        Assert.True(SpellbookService.TryNameDeck(wizard, 10, 5, 9, out var named));

        Assert.Same(deck, named);
        Assert.Equal(LockedPattern | 5, deck.m_pattern);
        Assert.Equal(9, deck.m_primaryColor);
    }

    [Fact]
    public void ArchmasteryIsSetOnTheEquippedDeck() {
        var equipped = Deck(11);
        var wizard = WizardWith(Deck(10), equipped);

        Assert.True(SpellbookService.TrySetDeckArchmastery(wizard, 11, 3, out _));

        Assert.True(CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(equipped, out var deckBehavior));
        Assert.Equal(3U, deckBehavior.m_archmasterySchool);
    }

    [Fact]
    public void DecksTheWizardDoesNotOwnOrItemsThatAreNotDecksAreRefused() {
        var notADeck = new WizClientObjectItem { m_globalID = 12, m_inactiveBehaviors = [] };
        var wizard = WizardWith(notADeck, Deck(11));

        Assert.False(SpellbookService.TryNameDeck(wizard, 99, 5, 9, out _));
        Assert.False(SpellbookService.TrySetDeckArchmastery(wizard, 12, 3, out _));
        Assert.Equal(0, notADeck.m_pattern);
    }

}
