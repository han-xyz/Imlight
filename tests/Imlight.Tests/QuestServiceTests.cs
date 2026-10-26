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
 * QUEST SERVICE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for resuming a player's quests when a template is missing
 * from the data pack, caching quest offers, goal totals, turn-in state and
 * the rewards a quest shows and grants.
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

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using Xunit;

namespace Imlight.Tests;

public sealed class QuestServiceTests {

    [Fact]
    public void NpcRefresh_QuestCompletion_SendsOneRefresh() {
        var gate = new NpcRefreshGate();

        Assert.True(gate.Request());
        Assert.False(gate.Request());
        Assert.False(gate.Request("WC-ST01-NPC03_Persona"));

        Assert.True(gate.TryTake(out var personaNames));
        Assert.Equal([null], personaNames);
        Assert.False(gate.TryTake(out _));
    }

    [Fact]
    public void NpcRefresh_RequestedWhileDialogOpen_SentAfterClose() {
        var gate = new NpcRefreshGate();
        gate.DialogShown();

        Assert.True(gate.Request());
        Assert.False(gate.TryTake(out _));

        Assert.True(gate.DialogClosed(out var personaNames));
        Assert.Equal([null], personaNames);
    }

    [Fact]
    public void NpcRefresh_PersonaOnly_TargetsThatPersona() {
        var gate = new NpcRefreshGate();

        Assert.True(gate.Request("WC-ST01-NPC03_Persona"));

        Assert.True(gate.TryTake(out var personaNames));
        Assert.Equal(["WC-ST01-NPC03_Persona"], personaNames);
    }

    [Fact]
    public void QuestResume_MissingTemplate_SkipsAndSendsRest() {
        var templates = new Dictionary<string, QuestTemplate> {
            ["First"] = new() { m_questName = "First" },
            ["Third"] = new() { m_questName = "Third" },
        };
        QuestInstance[] instances = [
            new() { QuestName = "First" },
            new() { QuestName = "Removed" },
            new() { QuestName = "Third" },
        ];

        var (toSend, skipped) = QuestService.PlanQuestResume(instances, name => templates.GetValueOrDefault(name));

        Assert.Equal(["First", "Third"], toSend.Select(q => q.Instance.QuestName));
        Assert.Same(templates["Third"], toSend[1].Template);
        Assert.Equal(["Removed"], skipped);
    }

    [Fact]
    public void QuestOfferCache_NoDuplicates() {
        var offers = new List<QuestTemplate>();

        QuestService.AddQuestOffer(offers, new QuestTemplate { m_questName = "Offered" });
        QuestService.AddQuestOffer(offers, new QuestTemplate { m_questName = "Offered" });
        QuestService.AddQuestOffer(offers, new QuestTemplate { m_questName = "Other" });

        Assert.Equal(["Offered", "Other"], offers.Select(q => (string) q.m_questName));
    }

    [Fact]
    public void BountyGoal_WithoutTallyCount_CompletesAtBountyTotal() {
        var goal = new BountyGoalTemplate {
            m_goalName = "DefeatPirates",
            m_goalType = GOAL_TYPE.GOAL_TYPE_BOUNTY,
            m_bountyTotal = 3,
            m_tallyCounter = new TallyCounterTemplate { m_count = 0 },
        };
        var progress = new GoalInstance(goal, 1);
        progress.BeginGoal();

        progress.IncrementGoal();
        progress.IncrementGoal();
        Assert.False(QuestService.HasReachedGoalTotal(goal, progress));

        progress.IncrementGoal();
        Assert.True(QuestService.HasReachedGoalTotal(goal, progress));
        Assert.Equal(3, QuestService.GetGoalTotal(goal));
    }

    [Fact]
    public void BountyGoal_WithoutTallyCount_MadlibTotalsAreBountyTotal() {
        var goal = new BountyGoalTemplate {
            m_goalName = "DefeatPirates",
            m_goalType = GOAL_TYPE.GOAL_TYPE_BOUNTY,
            m_bountyTotal = 3,
            m_tallyCounter = new TallyCounterTemplate { m_count = 0 },
        };

        var block = QuestMadlibs.GetAppropriateMadlibBlockForGoal(goal, new GoalInstance(goal, 1));

        var totals = block.m_madlibs.OfType<MadlibArgT_int>()
            .Where(arg => arg.m_madlibToken == "TOTAL" || arg.m_madlibToken == "SUBSCRIBER_TOTAL")
            .Select(arg => arg.m_madlibArgument);
        Assert.Equal([3, 3], totals);
    }

    [Fact]
    public void StartQuest_ThroughWizardFactory_StoresRolledRewards() {
        var template = EmptyQuest("Tutorial");
        var wizard = new Wizard { CharId = 1 };

        var started = QuestService.StartQuestInstance(template, wizard, null, null, _ => true);

        Assert.NotNull(started.Rewards);
    }

    [Theory]
    [InlineData(0f, 0.0)]
    [InlineData(150f, 1.0)]
    [InlineData(100f, 1.0)]
    [InlineData(50f, 0.5)]
    [InlineData(1f, 1.0)]
    [InlineData(0.25f, 0.25)]
    public void CollectChance_PercentOrFraction_NormalisedToFraction(float percentChance, double expected) {
        Assert.Equal(expected, QuestService.NormaliseCollectChance(percentChance), 6);
    }

    [Fact]
    public void TurnIn_KillsOutstandingAndPersonaLast_NotReady() {
        var quest = QuestWithKillThenPersona();
        quest.StartGoal("Kill");
        quest.StartGoal("TalkToNpc");

        Assert.False(quest.IsReadyForTurnIn());
    }

    [Fact]
    public void TurnIn_KillsDoneAndPersonaActive_Ready() {
        var quest = QuestWithKillThenPersona();
        quest.StartGoal("Kill");
        quest.CompleteGoal("Kill");
        quest.StartGoal("TalkToNpc");

        Assert.True(quest.IsReadyForTurnIn());
    }

    [Fact]
    public void TurnIn_UntakenBranchGoalAndPersonaActive_Ready() {
        var quest = QuestWithKillThenPersona();
        quest.StartGoal("TalkToNpc");

        Assert.True(quest.IsReadyForTurnIn());
    }

    [Fact]
    public void StartQuest_AlreadyHeld_AddsNothing() {
        var template = EmptyQuest("Pesky Pirates");
        var behavior = new ServerQuestBehavior();

        var first = QuestService.StartQuestInstance(template, 1, _ => new DropTableResult(), behavior.AddQuest);
        var second = QuestService.StartQuestInstance(template, 1, _ => new DropTableResult(), behavior.AddQuest);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Same(first, Assert.Single(behavior.CurrentQuestInstances));
    }

    [Fact]
    public void AcceptQuest_RequirementFails_Refused() {
        var template = EmptyQuest("Gated");

        var refusal = QuestService.GetAcceptRefusal(template, _ => false, _ => false, _ => false);

        Assert.Equal("the quest requirements are not met", refusal);
    }

    [Theory]
    [InlineData(true, false, "the quest is already held")]
    [InlineData(false, true, "the quest is already completed")]
    [InlineData(false, false, null)]
    public void AcceptQuest_HeldOrCompleted_Refused(bool held, bool completed, string expected) {
        var template = EmptyQuest("Pesky Pirates");

        var refusal = QuestService.GetAcceptRefusal(template, _ => held, _ => completed, _ => true);

        Assert.Equal(expected, refusal);
    }

    [Fact]
    public void QuestRewards_StartSendComplete_SameItems() {
        var template = EmptyQuest("Rewarded");
        template.m_endResults = new ResultList {
            m_results = [new ResDropTable { m_tableName = "RewardTable" }, new ResLearnSpell { m_templateID = 7 }],
        };
        var rolls = 0;
        DropTableResult Roll(QuestTemplate _) => new() {
            Items = [new DropItemResult { ItemId = $"item-{++rolls}" }],
        };

        var started = QuestService.StartQuestInstance(template, 1, Roll, _ => true);
        var shown = QuestService.ResolveQuestRewards(started, template, Roll);
        var (results, granted) = QuestService.PlanQuestCompletion(started, template.m_endResults);

        Assert.Equal(["item-1"], shown.Items.Select(i => i.ItemId));
        Assert.Equal(["item-1"], granted.Items.Select(i => i.ItemId));
        Assert.DoesNotContain(results.m_results, r => r is ResDropTable);
        Assert.Contains(results.m_results, r => r is ResLearnSpell);
    }

    [Fact]
    public void QuestRewards_InstanceWithoutStoredRewards_RollsAtCompletion() {
        var template = EmptyQuest("Legacy");
        template.m_endResults = new ResultList { m_results = [new ResDropTable { m_tableName = "RewardTable" }] };
        var legacy = new QuestInstance { QuestName = "Legacy" };

        var (results, granted) = QuestService.PlanQuestCompletion(legacy, template.m_endResults);

        Assert.Null(granted);
        Assert.Same(template.m_endResults, results);
    }

    private static QuestTemplate EmptyQuest(string name)
        => new() { m_questName = name, m_goals = [], m_startGoals = [] };

    private static QuestInstance QuestWithKillThenPersona() {
        var template = EmptyQuest("Pesky Pirates");
        template.m_goals = [
            new BountyGoalTemplate { m_goalName = "Kill", m_goalType = GOAL_TYPE.GOAL_TYPE_BOUNTY, m_bountyTotal = 2 },
            new PersonaGoalTemplate { m_goalName = "TalkToNpc", m_goalType = GOAL_TYPE.GOAL_TYPE_PERSONA },
        ];

        return new QuestInstance(template, 1);
    }

}
