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
 * SPIRAL DOOR WORLDS TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the worlds the Spiral Door offers: worlds with a hub
 * zone and quests in the data pack, in retail order.
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

using System.Collections.Generic;
using Imlight.CoreLib.Game.Zone.Components;
using Xunit;

namespace Imlight.Tests;

public sealed class SpiralDoorWorldsTests {

    private static readonly Dictionary<string, string> s_hubZones = new() {
        ["Celestia"] = "Celestia/CL_Hub",
        ["DragonSpire"] = "DragonSpire/DS_Hub_Cathedral",
        ["Grizzleheim"] = "Grizzleheim/GH_MainHub",
        ["Krokotopia"] = "Krokotopia/KT_Hub",
        ["Marleybone"] = "Marleybone/MB_Hub",
        ["MooShu"] = "MooShu/MS_Hub",
        ["WizardCity"] = "WizardCity/WC_Hub",
        ["Wysteria"] = "Wysteria/PA_Hub",
    };

    private static string? HubZoneOf(string world) => s_hubZones.GetValueOrDefault(world);

    [Fact]
    public void Worlds_WithQuests_AreListedInRetailOrder() {
        var worlds = WorldTeleportDoorComponent.SelectWorlds(HubZoneOf, ["CL", "DS", "GH", "KT", "MB", "MS", "WC"]);

        Assert.Equal(["WizardCity", "Krokotopia", "Marleybone", "MooShu", "DragonSpire", "Grizzleheim", "Celestia"], worlds);
    }

    [Fact]
    public void Hub_WithoutQuests_IsLeftOut() {
        var worlds = WorldTeleportDoorComponent.SelectWorlds(HubZoneOf, ["WC", "KT"]);

        Assert.Equal(["WizardCity", "Krokotopia"], worlds);
    }
}
