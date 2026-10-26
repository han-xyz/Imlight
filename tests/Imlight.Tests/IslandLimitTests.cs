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
 * ISLAND LIMIT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the castle limit the client shows: Housing.MaxCastles read into the game stats.
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

using System;
using System.IO;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class IslandLimitTests {

    [Fact]
    public void GameStats_TakeMaxCastlesFromHousingSection() {
        Assert.Equal(3, IslandLimitWith("[Housing]\nMaxCastles = 3\n"));
    }

    [Fact]
    public void GameStats_DefaultToOneCastleWhenUnset() {
        Assert.Equal(1, IslandLimitWith("[Housing]\nMaxCastles =\n"));
    }

    [Fact]
    public void GameStats_TakeMaxCastlesFromEnvOverride() {
        Environment.SetEnvironmentVariable("IMLIGHT__Housing__MaxCastles", "2");
        try {
            Assert.Equal(2, IslandLimitWith("[Housing]\nMaxCastles = 1\n"));
        } finally {
            Environment.SetEnvironmentVariable("IMLIGHT__Housing__MaxCastles", null);
        }
    }

    private static int IslandLimitWith(string ini) {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, ini);
            ConfigurationManager.Initialize(iniFilePath);

            return new ServerWizGameStats(MagicSchool.Fire, 1).m_maximumNumberOfIslands;
        } finally {
            File.Delete(iniFilePath);
        }
    }

}
