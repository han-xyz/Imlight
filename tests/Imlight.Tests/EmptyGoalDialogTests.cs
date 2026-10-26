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
 * EMPTY GOAL DIALOG TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for a persona goal whose Completion dialog has no lines:
 * it is completed without a dialog and its quest log text still names the NPC.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj --filter EmptyGoalDialogTests
 *
 * NOTE:
 * The madlib test reads the extracted client root wad (ROOTWAD_DIR).
 *
 * TODO:
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class EmptyGoalDialogTests {

    private const string PersonaObjectName = "DS-ACAD1-NPC01";

    private static readonly string? s_rootWadDirectory = Environment.GetEnvironmentVariable("ROOTWAD_DIR");

    [Fact]
    public void HasContent_DialogWithoutEntries_IsFalse() {
        Assert.False(GoalCompletionDialog.HasContent(null));
        Assert.False(GoalCompletionDialog.HasContent(new ActorDialog { m_dialogTag = "Completion" }));
        Assert.False(GoalCompletionDialog.HasContent(new ActorDialog { m_dialogEntries = [] }));
    }

    [Fact]
    public void HasContent_EntriesWithoutText_IsFalse() {
        var dialog = new ActorDialog { m_dialogEntries = [new NPCDialogEntry { m_dialog = "" }] };

        Assert.False(GoalCompletionDialog.HasContent(dialog));
    }

    [Fact]
    public void HasContent_EntryWithText_IsTrue() {
        var dialog = new ActorDialog { m_dialogEntries = [new NPCDialogEntry { m_dialog = "QuestDialog_1" }] };

        Assert.True(GoalCompletionDialog.HasContent(dialog));
    }

    [Fact]
    public void HasContent_EntriesWithMadlib_IsTrue() {
        var dialog = new ActorDialog {
            m_dialogEntries = [new NPCDialogEntry()],
            m_madlibs = [new ActorMadlib { m_madlibBlock = new MadlibBlock() }],
        };

        Assert.True(GoalCompletionDialog.HasContent(dialog));
    }

    [GameDataFact]
    public void PersonaGoal_WithoutCompletionMadlibs_NamesThePersonaNpc() {
        LoadGameData();
        var goal = new PersonaGoalTemplate {
            m_goalName = "TalkTo",
            m_goalType = GOAL_TYPE.GOAL_TYPE_PERSONA,
            m_personaName = PersonaObjectName + "_Persona",
            m_dialogList = new ActorDialogList { m_dialogs = [new ActorDialog { m_dialogTag = "Completion" }] },
        };

        var block = QuestMadlibs.GetAppropriateMadlibBlockForGoal(goal, new GoalInstance(goal, 1));

        var names = block.m_madlibs.OfType<MadlibArgT_ByteString>()
            .Where(arg => arg.m_madlibToken == "FIRSTNAME" || arg.m_madlibToken == "LASTNAME")
            .ToDictionary(arg => arg.m_madlibToken, arg => arg.m_madlibArgument);
        Assert.False(string.IsNullOrEmpty(names["FIRSTNAME"]));
    }

    private static void LoadGameData() {
        InitializeQuietLogging();
        RootArchiveLoader.UseRootWad(ArchiveOf(s_rootWadDirectory!, ["TemplateManifest.xml"]));
        _ = CoreObjectFactory.Instance;
        var templateFiles = CoreObjectFactory.TemplateManifest.m_serializedTemplates
            .Where(location => location?.m_filename is not null
                && Path.GetFileNameWithoutExtension(location.m_filename) == PersonaObjectName)
            .Select(location => location.m_filename.ToString())
            .ToArray();

        RootArchiveLoader.UseRootWad(ArchiveOf(s_rootWadDirectory!, ["TemplateManifest.xml", "Locale/en-US/", .. templateFiles]));
        _ = Locale.Instance;
    }

    private static Archive ArchiveOf(string directory, string[] pathPrefixes) {
        var stream = new MemoryStream();
        var files = new Dictionary<string, FileEntry>();
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
            var name = Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/');
            if (!pathPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) {
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            files[name] = new FileEntry {
                Offset = (uint) stream.Position,
                UncompressedSize = (uint) bytes.Length,
                CompressedSize = (uint) bytes.Length,
                FileName = name,
            };
            stream.Write(bytes);
        }

        return new Archive(files, stream, version: 1);
    }

    private static void InitializeQuietLogging() {
        var iniFilePath = Path.GetTempFileName();
        var logPath = Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt");
        try {
            File.WriteAllText(iniFilePath, $"[Logging]\nLogPath = {logPath}\nLogLevel = ERROR\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    private sealed class GameDataFactAttribute : FactAttribute {
        public GameDataFactAttribute() {
            if (!Directory.Exists(s_rootWadDirectory)) {
                Skip = "set ROOTWAD_DIR to the extracted client root wad to run this test";
            }
        }
    }

}
