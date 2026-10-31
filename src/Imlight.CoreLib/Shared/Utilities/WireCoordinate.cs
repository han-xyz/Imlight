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
 * WIRE COORDINATE
 * ========================================================================
 * 
 * PURPOSE:
 * Encodes a world coordinate into the 16-bit position field of the teleport
 * and move messages.
 * 
 * USAGE EXAMPLE:
 * LocationX = WireCoordinate.Encode(position.X)
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System;

namespace Imlight.CoreLib.Shared.Utilities;

internal static class WireCoordinate {

    private const float COORDINATE_UNITS_PER_WIRE_UNIT = 4.0f;

    public static ushort Encode(float coordinate)
        => unchecked((ushort) (short) MathF.Round(coordinate / COORDINATE_UNITS_PER_WIRE_UNIT));
}
