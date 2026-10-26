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
 * TREASURE CARD TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for consuming a treasure card in a duel, cast or enchant.
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
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class TreasureCardTests {

    private const uint CardId = 4242;

    [Fact]
    public void SandboxedDuel_ConsumesTheVaultCopyOnly() {
        var spellbook = new ServerWizSpellbookBehavior();
        spellbook.AddTreasureCard(CardId);
        var vault = new CombatDeckSpellData { TemplateId = CardId, Quantity = 2, IsTreasureCard = true };
        var caster = new CombatDuelSubCircle(null!, 0, 0, default, 0) {
            _wizard = new Wizard { SpellbookBehavior = spellbook },
            _combatDeck = new CombatDeck([], [vault], 7),
            IsSandboxed = true
        };

        caster.ConsumeTreasureCard(new Spell { m_templateID = CardId, m_treasureCard = true });

        Assert.Equal(1u, vault.Quantity);
        Assert.Contains(CardId, spellbook.TreasureCardTemplateIds);
    }

}
