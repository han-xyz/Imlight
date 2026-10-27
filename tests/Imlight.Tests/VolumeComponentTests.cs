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
 * VOLUME COMPONENT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for which quest proximity goals a zone volume completes
 * when a player is inside it, including a goal that starts while they are.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * Volume and goal names come from WizardCity-WC_Golem_Tower.wad and the
 * Opt-Explorer-001 and Explore-001-3 quest templates; the Yardbirds names from
 * the Grizzleheim zone files, which share GH-Explorer-C01-001's misspellings.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System.Linq;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using Xunit;

namespace Imlight.Tests;

public sealed class VolumeComponentTests {

    private const string GolemTower = "WizardCity/WC_Golem_Tower";

    [Fact]
    public void ProximityGoal_TaggedWithEnterEvent_AttachesToVolume() {
        var quest = WaypointQuest("Opt-Explorer-001", "Opt-Explorer-001_Goal0", "Enter_Golem Court Smith", GolemTower);
        var volume = GolemCourtSmith();

        var goals = VolumeComponent.FindProximityGoals([quest], volume, GolemTower);

        Assert.Equal([("Opt-Explorer-001", "Opt-Explorer-001_Goal0")], goals);
    }

    [Theory]
    [InlineData("Golem Court Smith")]
    [InlineData("Enter_Golem Court Smith")]
    public void ProximityGoal_VolumeWithoutEnterEvents_AttachesByNameOrEnterName(string proximityTag) {
        var quest = WaypointQuest("Quest", "Goal", proximityTag, GolemTower);
        var volume = new Volume { m_volumeName = new ByteString("Golem Court Smith"), m_enterEvents = [] };

        var goals = VolumeComponent.FindProximityGoals([quest], volume, GolemTower);

        Assert.Equal([("Quest", "Goal")], goals);
    }

    [Theory]
    [InlineData("WizardCity/WC_Hub", false)]
    [InlineData("wizardcity/wc_golem_tower", true)]
    [InlineData("", true)]
    public void ProximityGoal_ZoneTag_MatchesThisZoneIgnoringCaseOrAnyZoneWhenEmpty(string zoneTag, bool attaches) {
        var quest = WaypointQuest("Quest", "Goal", "Enter_Golem Court Smith", zoneTag);

        var goals = VolumeComponent.FindProximityGoals([quest], GolemCourtSmith(), GolemTower);

        Assert.Equal(attaches, goals.Count == 1);
    }

    [Theory]
    [InlineData("GH-Exporer-C01-001QuestVolumeGoal4", "Grizzleheim/GH_Hero")]
    [InlineData("GH-Eplorer-C01-001QuestVolumeGoal6", "Grizzleheim/GH_RedClaw")]
    [InlineData("GH-Exporer-C01-001QuestVolumeGoal11", "Grizzleheim/GH_Raven")]
    public void ProximityGoal_YardbirdsMisspelledTag_AttachesToVolumeSpelledTheSame(string volumeName, string zonePath) {
        var quest = WaypointQuest("GH-Explorer-C01-001", "Goal", $"Enter_{volumeName}", zonePath);
        var volume = new Volume {
            m_volumeName = new ByteString(volumeName),
            m_enterEvents = [new ByteString($"Enter_{volumeName}")],
        };

        var goals = VolumeComponent.FindProximityGoals([quest], volume, zonePath);

        Assert.Equal([("GH-Explorer-C01-001", "Goal")], goals);
    }

    [Fact]
    public void ProximityGoal_OtherVolumeTag_DoesNotAttach() {
        var quest = WaypointQuest("Opt-Explorer-001", "Opt-Explorer-001_Goal1", "Enter_The Commons Smith", GolemTower);

        Assert.Empty(VolumeComponent.FindProximityGoals([quest], GolemCourtSmith(), GolemTower));
    }

    [Fact]
    public void ProximityGoal_QuestJustStarted_CompletesForPlayerInVolume() {
        var template = WaypointQuest("Opt-Explorer-001", "Opt-Explorer-001_Goal0", "Enter_Golem Court Smith", GolemTower);
        var wizard = new Wizard { CharId = 1, QuestBehavior = new ServerQuestBehavior() };
        var quest = QuestService.StartQuestInstance(template, 1, _ => new DropTableResult(), wizard.QuestBehavior.AddQuest);

        var completion = VolumeComponent.ProximityGoalCompletion(wizard, "Opt-Explorer-001", "Opt-Explorer-001_Goal0");

        Assert.NotNull(completion);
        Assert.Equal(quest.ID, completion.QuestID);
        Assert.Equal(quest.GoalProgress.Single().ID, completion.GoalID);
    }

    [Fact]
    public void ProximityGoal_AlreadyCompleted_IsNotCompletedAgain() {
        var template = WaypointQuest("Opt-Explorer-001", "Opt-Explorer-001_Goal0", "Enter_Golem Court Smith", GolemTower);
        var wizard = new Wizard { CharId = 1, QuestBehavior = new ServerQuestBehavior() };
        var quest = QuestService.StartQuestInstance(template, 1, _ => new DropTableResult(), wizard.QuestBehavior.AddQuest);
        quest.CompleteGoal("Opt-Explorer-001_Goal0");

        Assert.Null(VolumeComponent.ProximityGoalCompletion(wizard, "Opt-Explorer-001", "Opt-Explorer-001_Goal0"));
    }

    private static Volume GolemCourtSmith() => new() {
        m_volumeName = new ByteString("Golem Court Smith"),
        m_enterEvents = [new ByteString("Enter_Golem Court Smith")],
        m_exitEvents = [new ByteString("Exit_Golem Court Smith")],
    };

    private static QuestTemplate WaypointQuest(string questName, string goalName, string proximityTag, string zoneTag) => new() {
        m_questName = questName,
        m_startGoals = [goalName],
        m_goals = [new WaypointGoalTemplate {
            m_goalName = goalName,
            m_proximityTag = proximityTag,
            m_zoneTag = zoneTag,
        }],
    };

}
