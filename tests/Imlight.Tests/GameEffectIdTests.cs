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
 * GAME EFFECT ID TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for a wizard's effect internal IDs: a new effect never takes the ID of one it
 * keeps, as a mount's speed effect did when it came back after a fight.
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

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class GameEffectIdTests {

    [Fact]
    public void NoEffects_StartsAtZero() {
        var wizard = new Wizard();

        Assert.Equal(0, wizard.NextEffectInternalId());
    }

    [Fact]
    public void AfterARemoval_NextIdIsUnused() {
        var wizard = new Wizard();
        wizard.GameEffects.AddRange([
            new NamedEffect { m_internalID = 0 },
            new NamedEffect { m_internalID = 2 },
        ]);

        Assert.Equal(3, wizard.NextEffectInternalId());
    }

}
