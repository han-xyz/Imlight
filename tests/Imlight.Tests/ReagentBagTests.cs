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
 * REAGENT BAG TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the reagent stack limit applied to a bag loaded from
 * the player database.
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

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;
using Xunit;

namespace Imlight.Tests;

public sealed class ReagentBagTests {

    [Fact]
    public void LoadedStack_OverTheLimit_IsCappedAndOthersKept() {
        var duplicated = new ClientReagentItem { m_quantity = 2047 };
        var normal = new ClientReagentItem { m_quantity = 12 };

        var capped = ServerAlchemyBehavior.CapLoadedStacks([duplicated, normal]);

        Assert.Equal(1, capped);
        Assert.Equal(ServerAlchemyBehavior.MaxReagentStack, duplicated.m_quantity);
        Assert.Equal(12, normal.m_quantity);
    }

}
