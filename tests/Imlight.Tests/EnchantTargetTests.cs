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
 * ENCHANT TARGET TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for which cards an enchantment may be applied to.
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
using Imlight.CoreLib.Game.Zone.Components;
using Xunit;

namespace Imlight.Tests;

public sealed class EnchantTargetTests {

    private const uint ListedCard = 100;
    private const uint OtherCard = 200;

    private static SpellTemplate EnchantWith(SpellEffect effect) => new() { m_effects = [effect] };

    [Fact]
    public void Enchant_SpecificSpellsTarget_NotListed_Rejected() {
        var mutation = EnchantWith(new SpellEffect {
            m_effectType = kSpellEffects.kModifyCardMutation,
            m_effectTarget = kEffectTarget.kSpecificSpells,
            m_spellTemplateID = ListedCard,
            m_effectParam = 300
        });
        var target = new SpellTemplate();

        Assert.True(CombatDuelComponent.EnchantEffectsAllow(mutation, target, ListedCard));
        Assert.False(CombatDuelComponent.EnchantEffectsAllow(mutation, target, OtherCard));
    }

    [Fact]
    public void Enchant_UnhandledEffect_Rejected() {
        var unhandled = EnchantWith(new SpellEffect { m_effectType = kSpellEffects.kDamage });

        Assert.False(CombatDuelComponent.EnchantEffectsAllow(unhandled, new SpellTemplate(), ListedCard));
    }
}
