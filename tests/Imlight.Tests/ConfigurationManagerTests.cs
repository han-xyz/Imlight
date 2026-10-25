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
 * CONFIGURATION MANAGEMENT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for reading INI settings through ConfigurationManager.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.IO;
using Imlight.Common;
using Xunit;

namespace Imlight.Tests;

public sealed class ConfigurationManagerTests {

    [Fact]
    public void Initialize_ReadsSectionKey() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Server]\nPort = 12000\n");

            ConfigurationManager.Initialize(iniFilePath);

            Assert.Equal("12000", (string) ConfigurationManager.Settings["Server.Port"]);
            Assert.Equal(12000, ConfigurationManager.Settings["Server.Port"].AsInt());
        } finally {
            File.Delete(iniFilePath);
        }
    }

}
