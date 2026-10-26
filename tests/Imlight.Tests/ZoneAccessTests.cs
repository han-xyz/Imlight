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
 * ZONE ACCESS TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the zone gate that keeps normal accounts out of QA zones.
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

using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class ZoneAccessTests {

    private const string QaZone = "WizardCity/QA_SpawnRate";
    private const string HubZone = "WizardCity/WC_Hub";
    private const string StartingZone = "WizardCity/Interiors/WC_Headmistress_House";

    [Fact]
    public void NormalAccount_IsRefusedQaZone()
        => Assert.False(ZoneAccess.IsAllowed(AuthLevel.None, "WizardCity/QA_SpawnRate"));

    [Theory]
    [InlineData(AuthLevel.QualityAssurance)]
    [InlineData(AuthLevel.Administrator)]
    public void StaffAccount_EntersQaZone(AuthLevel authLevel)
        => Assert.True(ZoneAccess.IsAllowed(authLevel, "WizardCity/QA_SpawnRate"));

    [Fact]
    public void NormalAccount_EntersOrdinaryZone()
        => Assert.True(ZoneAccess.IsAllowed(AuthLevel.None, "WizardCity/WC_Hub"));

    [Fact]
    public void MissingAccount_IsRefusedQaZone()
        => Assert.False(ZoneAccess.Admits(null, QaZone));

    [Fact]
    public void NormalAccountAttach_ToQaZone_FallsBackToSavedZone()
        => Assert.Equal(HubZone, ZoneAccess.AttachZone(Account(AuthLevel.None), QaZone, HubZone, StartingZone));

    [Fact]
    public void NormalAccountAttach_SavedInQaZone_FallsBackToStartingZone()
        => Assert.Equal(StartingZone, ZoneAccess.AttachZone(Account(AuthLevel.None), QaZone, QaZone, StartingZone));

    [Fact]
    public void MissingAccountAttach_ToQaZone_FallsBackToStartingZone()
        => Assert.Equal(StartingZone, ZoneAccess.AttachZone(null, QaZone, "", StartingZone));

    [Fact]
    public void StaffAccountAttach_KeepsQaZone()
        => Assert.Equal(QaZone, ZoneAccess.AttachZone(Account(AuthLevel.QualityAssurance), QaZone, HubZone, StartingZone));

    private static Account Account(AuthLevel authLevel)
        => new() { AuthLevel = authLevel };

}
