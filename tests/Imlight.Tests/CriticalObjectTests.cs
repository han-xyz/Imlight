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
 * CRITICAL OBJECT TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for the placed objects a client waits for before it shows its HUD.
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
 * Last Updated: 10/27/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Supervisors;
using Xunit;

namespace Imlight.Tests;

public sealed class CriticalObjectTests {

    // Unicorn Way's shopkeeper placement (template 87112) is both critical and undetectable.
    private static readonly GameObjectTemplate CriticalNpc = new() { m_adjectiveList = ["WC-ST01-NPC08_Shop.AdjRef", "Critical"] };
    private static readonly GameObjectTemplate PlainNpc = new() { m_adjectiveList = ["WC-ST01-NPC02.AdjRef"] };

    [Fact]
    public void ACriticalTemplate_IsCritical() {
        Assert.True(ZoneObjectSupervisor.IsCriticalObject(CriticalNpc, new CoreObjectInfo { m_bUndetectable = false }));
    }

    [Fact]
    public void AnUndetectablePlacement_IsNotCritical() {
        Assert.False(ZoneObjectSupervisor.IsCriticalObject(CriticalNpc, new CoreObjectInfo { m_bUndetectable = true }));
    }

    [Fact]
    public void ATemplateWithoutTheAdjective_IsNotCritical() {
        Assert.False(ZoneObjectSupervisor.IsCriticalObject(PlainNpc, new CoreObjectInfo()));
    }

}
