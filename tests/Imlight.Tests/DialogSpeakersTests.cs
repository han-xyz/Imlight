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
 * DIALOG SPEAKERS TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for the dialog a client is sent: a line only one school's wizards
 * hear reaches that school alone, and its counterpart every other school.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj --filter DialogSpeakersTests
 *
 * NOTE:
 * The requirement dispatcher logs, and the logger reads its settings from ConfigurationManager.
 *
 * TODO:
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/28/2026
 */

using System.IO;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class DialogSpeakersTests {

    public DialogSpeakersTests() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Logging]\nLogPath = ./logs/log.txt\nLogLevel = ERROR\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    [Fact]
    public void ForClient_LineForOneSchool_ReachesThatSchoolAlone() {
        var dialog = SchoolDialog();

        Assert.Equal(["Life only", "Everyone"], LinesOf(DialogSpeakers.ForClient(dialog, WizardOf(MagicSchool.Life))));
        Assert.Equal(["Not Life", "Everyone"], LinesOf(DialogSpeakers.ForClient(dialog, WizardOf(MagicSchool.Fire))));
    }

    [Fact]
    public void ForClient_WithoutWizard_KeepsEveryLine() {
        Assert.Equal(["Life only", "Not Life", "Everyone"], LinesOf(DialogSpeakers.ForClient(SchoolDialog())));
    }

    private static ActorDialog SchoolDialog()
        => new() {
            m_dialogTag = "Completion",
            m_dialogEntries = [
                new NPCDialogEntry { m_dialog = "Life only", m_requirements = SchoolOfFocus("Life", applyNot: false) },
                new NPCDialogEntry { m_dialog = "Not Life", m_requirements = SchoolOfFocus("Life", applyNot: true) },
                new NPCDialogEntry { m_dialog = "Everyone" },
            ],
        };

    private static RequirementList SchoolOfFocus(string school, bool applyNot)
        => new() { m_requirements = [new ReqSchoolOfFocus { m_magicSchool = school, m_applyNOT = applyNot }] };

    private static Wizard WizardOf(MagicSchool school)
        => new() { MagicSchoolBehavior = new ServerMagicSchoolBehavior { MagicSchool = school } };

    private static string[] LinesOf(ActorDialog dialog)
        => [.. dialog.m_dialogEntries.Select(entry => (string) entry.m_dialog)];

}
