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
 * SPELL NAME ALIAS TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for resolving a spell name the client data misspells.
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;
using Xunit;

namespace Imlight.Tests;

public class SpellNameAliasTests {

    [Fact]
    public void MisspelledCreatureSpell_ResolvesToTheRealTemplate() {
        var realHash = StringHash.Compute("NA Fire Elf-01");
        SpellFactory.s_spellTemplates[realHash] = new SpellTemplate();

        Assert.True(SpellFactory.TryGetTemplateHash("Natural Fire Ellf", out var hash));
        Assert.Equal(realHash, hash);
    }

    [Fact]
    public void UnknownSpellName_DoesNotResolve()
        => Assert.False(SpellFactory.TryGetTemplateHash("No Such Spell", out _));
}
