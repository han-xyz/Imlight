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
 * WIRE COORDINATE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the position encoding sent in teleport and move
 * messages: negative coordinates must survive the trip.
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
 * Last Updated: 10/31/2026
 */

using Imlight.CoreLib.Shared.Utilities;
using Xunit;

namespace Imlight.Tests;

public sealed class WireCoordinateTests {

    [Theory]
    [InlineData(-7358f)]
    [InlineData(-7359f)]
    [InlineData(-10041f)]
    [InlineData(-968f)]
    public void Encode_NegativeCoordinate_DecodesBackWithinRounding(float coordinate) {
        var decoded = (short) WireCoordinate.Encode(coordinate) * 4f;

        Assert.InRange(decoded, coordinate - 2f, coordinate + 2f);
    }

    [Fact]
    public void Encode_PositiveCoordinate_IsScaledUnchanged() {
        Assert.Equal((ushort) 250, WireCoordinate.Encode(1000f));
    }
}
