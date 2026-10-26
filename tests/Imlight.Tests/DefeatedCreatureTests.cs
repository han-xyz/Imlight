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
 * DEFEATED CREATURE TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for which creatures may start a duel.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imlight.CoreLib.Game.Zone.Components;
using Xunit;

namespace Imlight.Tests;

public sealed class DefeatedCreatureTests {

    [Fact]
    public void ADefeatedCreatureCannotStartADuel()
        => Assert.False(NpcComponent.MayStartDuel(isMonster: true, isDefeated: true));

    [Fact]
    public void ARespawnedCreatureCanStartADuel()
        => Assert.True(NpcComponent.MayStartDuel(isMonster: true, isDefeated: false));
}
