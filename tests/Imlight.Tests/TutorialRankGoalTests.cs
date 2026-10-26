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
 * TUTORIAL RANK GOAL TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Characterises the data pack's ACHIEVERANK goals: every one is a tutorial
 * beat the client completes by name (RequestGoalCompletion), and the goal
 * lookup and completion TutorialService runs for that request resolve it.
 * 
 * USAGE EXAMPLE:
 * SPIRALDB_CLONE=<spiraldb clone> dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
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
using System.Linq;
using System.Text.Json;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class TutorialRankGoalTests {

    private const ulong OWNER_CHAR_ID = 1;

    private static readonly string? s_spiralDbClone = Environment.GetEnvironmentVariable("SPIRALDB_CLONE");

    [PackFact]
    public void AchieveRankGoals_AreTutorialBeats_CompletedByName() {
        var rankQuests = Directory.GetFiles(Path.Combine(s_spiralDbClone!, "QuestTemplates"), "*.json")
            .Where(file => File.ReadAllText(file).Contains("GOAL_TYPE_ACHIEVERANK"))
            .Select(file => JsonSerializer.Deserialize<QuestTemplate>(File.ReadAllText(file), SpiralJsonOptions.Options)!)
            .ToList();

        Assert.Equal(["Tutorial_Intro", "WC-TUT-C03-001", "WC-TUT-C05-001", "WC-TUT-C08-001"],
            rankQuests.Select(q => (string) q.m_questName).Order());

        foreach (var quest in rankQuests) {
            var rankGoals = quest.m_goals.OfType<AchieveRankGoalTemplate>().ToList();
            Assert.All(rankGoals, goal => Assert.Equal(1, goal.m_rank));

            var questBehavior = new ServerQuestBehavior();
            questBehavior.AddQuest(new QuestInstance(quest, OWNER_CHAR_ID));

            foreach (var goal in rankGoals) {
                var match = questBehavior.CurrentQuestInstances
                    .SelectMany(q => q.GoalProgress, (q, progress) => (Quest: q, Progress: progress))
                    .First(x => x.Progress.GoalName == goal.m_goalName);

                Assert.True(questBehavior.CompleteQuestGoal(match.Quest.QuestName, goal.m_goalName));
                Assert.True(match.Progress.IsGoalCompleted());
            }
        }
    }

    private sealed class PackFactAttribute : FactAttribute {
        public PackFactAttribute() {
            if (s_spiralDbClone is null || !Directory.Exists(s_spiralDbClone)) {
                Skip = "set SPIRALDB_CLONE to a spiraldb clone to run this test";
            }
        }
    }

}
