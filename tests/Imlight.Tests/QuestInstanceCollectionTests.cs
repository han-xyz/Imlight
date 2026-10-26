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
 * QUEST INSTANCE COLLECTION TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the settings the quest instance collection reads.
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
using Imlight.CoreLib.WizardData.Collections;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class QuestInstanceCollectionTests {

    private const string LoggingSection
        = "[Logging]\nLogPath = ./logs/log.txt\nLogLevel = ERROR\nLogFormat = {Message:lj}{NewLine}\nSeqSinkUrl = http://localhost:5341\n";

    [Theory]
    [InlineData("[Database]\nDatabaseWaitForNonStaleResultsTimeout = 7\n", 7)]
    [InlineData("[Database]\n", 2)]
    public void NonStaleWaitTimeout_ReadFromDatabaseSetting(string databaseSection, int expectedSeconds) {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, LoggingSection + databaseSection);
            ConfigurationManager.Initialize(iniFilePath);

            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), QuestInstanceCollection.NonStaleWaitTimeout);
        } finally {
            File.Delete(iniFilePath);
        }
    }

}
