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
 * ITEM CARD TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for the cards equipment gives: a stats recalculation drops them before the
 * equipped items give them again, and keeps the temporary spells quests and staff add.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;
using Xunit;

namespace Imlight.Tests;

public sealed class ItemCardTests {

    [Fact]
    public void RemoveItemCards_KeepsOtherTemporarySpells() {
        var spellbook = new ServerWizSpellbookBehavior();
        spellbook.AddTemporarySpellToBook(new Spell { m_templateID = 1, m_itemCard = true });
        spellbook.AddTemporarySpellToBook(new Spell { m_templateID = 1, m_itemCard = true });
        spellbook.AddTemporarySpellToBook(new Spell { m_templateID = 2 });

        spellbook.RemoveItemCards();

        Assert.Equal([2u], spellbook.TemporarySpells.Select(spell => spell.m_templateID));
    }

}
