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

using System;
using System.IO;
using Imlight.Common;
using Serilog.Events;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
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

    [Fact]
    public void EnvOverride_WinsOverIni() {
        var value = ReadWithEnv("[Logging]\nLogLevel = DEBUG\n", "IMLIGHT__Logging__LogLevel", "INFO", "Logging.LogLevel");

        Assert.Equal("INFO", value);
    }

    [Fact]
    public void EnvOverride_SectionWithSpace() {
        var iniFilePath = WriteIni("[Game Server]\nGameServerIP = 127.0.0.1\n");
        try {
            Environment.SetEnvironmentVariable("IMLIGHT__Game_Server__GameServerIP", "10.0.0.5");

            ConfigurationManager.Initialize(iniFilePath);

            Assert.Equal("10.0.0.5", (string) ConfigurationManager.Settings["Game Server.GameServerIP"]);
            Assert.Equal("10.0.0.5", ConfigurationManager.GetSection("Game Server")["GameServerIP"]);
        } finally {
            Environment.SetEnvironmentVariable("IMLIGHT__Game_Server__GameServerIP", null);
            File.Delete(iniFilePath);
        }
    }

    [Fact]
    public void MissingEnv_KeepsIni() {
        var iniFilePath = WriteIni("[Logging]\nLogLevel = DEBUG\n");
        try {
            ConfigurationManager.Initialize(iniFilePath);

            Assert.Equal("DEBUG", (string) ConfigurationManager.Settings["Logging.LogLevel"]);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    [Fact]
    public void EnvOverride_MalformedIgnored() {
        var logPath = Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt");
        var iniFilePath = WriteIni($"[Logging]\nLogPath = {logPath}\nLogLevel = DEBUG\nLogFormat = {{Message:lj}}{{NewLine}}\nSeqSinkUrl = http://localhost:5341\n");
        string[] malformedNames = ["IMLIGHT__Nope", "IMLIGHT__Nope1", "IMLIGHT__Nope2", "IMLIGHT__Nope3", "IMLIGHT__Nope4", "IMLIGHT__Nope5", "IMLIGHT__Nope6", "IMLIGHT__Nope7"];
        try {
            foreach (var name in malformedNames) {
                Environment.SetEnvironmentVariable(name, "1");
            }
            Environment.SetEnvironmentVariable("IMLIGHT__Logging__LogLevel", "ERROR");

            ConfigurationManager.Initialize(iniFilePath);

            Assert.Equal(["Logging"], ConfigurationManager.GetSectionNames());
            Assert.Equal("ERROR", (string) ConfigurationManager.Settings["Logging.LogLevel"]);
            Assert.False(Logger.Log.IsEnabled(LogEventLevel.Debug));
        } finally {
            foreach (var name in malformedNames) {
                Environment.SetEnvironmentVariable(name, null);
            }
            Environment.SetEnvironmentVariable("IMLIGHT__Logging__LogLevel", null);
            File.Delete(iniFilePath);
        }
    }

    [Theory]
    [InlineData("IMLIGHT__A__B", true, "A", "B")]
    [InlineData("IMLIGHT__Game_Server__X", true, "Game Server", "X")]
    [InlineData("IMLIGHT__A", false, "", "")]
    [InlineData("IMLIGHT____B", false, "", "")]
    [InlineData("OTHER__A__B", false, "", "")]
    [InlineData("IMLIGHT__Game__Server__X", false, "", "")]
    public void TryParseOverrideName_Matrix(string name, bool expected, string expectedSection, string expectedKey) {
        var parsed = ConfigurationManager.TryParseOverrideName(name, out var section, out var key);

        Assert.Equal(expected, parsed);
        if (expected) {
            Assert.Equal(expectedSection, section);
            Assert.Equal(expectedKey, key);
        }
    }

    private static string WriteIni(string contents) {
        var iniFilePath = Path.GetTempFileName();
        File.WriteAllText(iniFilePath, contents);
        return iniFilePath;
    }

    private static string ReadWithEnv(string ini, string variable, string variableValue, string settingKey) {
        var iniFilePath = WriteIni(ini);
        try {
            Environment.SetEnvironmentVariable(variable, variableValue);

            ConfigurationManager.Initialize(iniFilePath);

            return ConfigurationManager.Settings[settingKey];
        } finally {
            Environment.SetEnvironmentVariable(variable, null);
            File.Delete(iniFilePath);
        }
    }

}
