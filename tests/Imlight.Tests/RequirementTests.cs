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
 * REQUIREMENT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for requirements evaluated against a wizard through the
 * requirement dispatcher.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * The dispatcher logs, and the logger reads its settings from ConfigurationManager.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;
using Type = System.Type;

namespace Imlight.Tests;

[Collection("env")]
public sealed class RequirementTests {

    public RequirementTests() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Logging]\nLogPath = ./logs/log.txt\nLogLevel = ERROR\nLogFormat = {Message:lj}{NewLine}\nSeqSinkUrl = http://localhost:5341\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void ReqMagicLevel_HandlerRegistered_EvaluatesLevel(int wizardLevel, bool expected) {
        var wizard = new Wizard { MagicSchoolBehavior = new ServerMagicSchoolBehavior { Level = wizardLevel } };
        var requirements = new RequirementList {
            m_requirements = [
                new ReqMagicLevel { m_numericValue = 10, m_operatorType = OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ },
            ],
        };

        var met = RequirementDispatcher.EvaluateRequirements(requirements, new GenericRequirementContext(requirements, null, null, wizard));

        Assert.Equal(expected, met);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void QuestOffer_PruneRequirementsMet_NotOffered(bool entrySet, bool offered) {
        var quest = new QuestTemplate { m_questName = "Side", m_pruneRequirements = HasEntry("Main_Complete") };
        var wizard = new Wizard { QuestBehavior = new ServerQuestBehavior() };
        if (entrySet) {
            wizard.QuestBehavior.AddToRegistry("Main_Complete", 1);
        }

        var quests = InteractQuestOfferComponent.FindAvailableQuests([quest], wizard);

        Assert.Equal(offered, quests.Contains(quest));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void QuestOffer_PrepRequirementsUnmet_NotOffered(bool entrySet, bool offered) {
        var quest = new QuestTemplate { m_questName = "Side", m_prepRequirements = HasEntry("Prepared") };
        var wizard = new Wizard { QuestBehavior = new ServerQuestBehavior() };
        if (entrySet) {
            wizard.QuestBehavior.AddToRegistry("Prepared", 1);
        }

        var quests = InteractQuestOfferComponent.FindAvailableQuests([quest], wizard);

        Assert.Equal(offered, quests.Contains(quest));
    }

    [Theory]
    [InlineData(typeof(ReqHasEntry))]
    [InlineData(typeof(ReqSchoolOfFocus))]
    [InlineData(typeof(ReqMagicLevel))]
    [InlineData(typeof(ReqInZone))]
    [InlineData(typeof(ReqHasQuest))]
    [InlineData(typeof(ReqIsSchool))]
    [InlineData(typeof(ReqGlobalRegistryValue))]
    [InlineData(typeof(ReqHasGoal))]
    [InlineData(typeof(ReqClassProjectActive))]
    [InlineData(typeof(ReqHasSpell))]
    [InlineData(typeof(ReqHasBadge))]
    [InlineData(typeof(ReqEnergy))]
    [InlineData(typeof(ReqHealthPercent))]
    [InlineData(typeof(ReqManaPercent))]
    [InlineData(typeof(ReqGardeningLevel))]
    [InlineData(typeof(ReqInCombat))]
    [InlineData(typeof(ReqPvPCombat))]
    [InlineData(typeof(ReqPipCount))]
    [InlineData(typeof(ReqShadowPipCount))]
    [InlineData(typeof(ReqIsGender))]
    [InlineData(typeof(ReqIsQuest))]
    [InlineData(typeof(ReqIsGoal))]
    [InlineData(typeof(ReqInventoryFull))]
    [InlineData(typeof(ReqHighestCharacterLevelOnAccount))]
    [InlineData(typeof(ReqOtherCharacterQuestCompleted))]
    [InlineData(typeof(ReqHasSecondarySchool))]
    public void AllDataPackRequirementTypes_HaveHandlers(Type requirementType) {
        var requirements = new RequirementList { m_requirements = [(Requirement) Activator.CreateInstance(requirementType)!] };
        var context = new GenericRequirementContext(requirements, null, null, new Wizard());

        Assert.NotNull(RequirementDispatcher.FindHandlerForRequirement(requirementType, context));
    }

    [Theory]
    [InlineData(false, 11, true)]
    [InlineData(false, 12, false)]
    [InlineData(true, 55, true)]
    [InlineData(true, 60, false)]
    public void ReqEnergy_ComparesEnergyOrItsPercentOfMax(bool isPercent, float required, bool expected) {
        var wizard = new Wizard { GameStats = new ServerWizGameStats(MagicSchool.Fire, 1) { m_energyMax = 20 }, PetOwnerBehavior = new ServerPetOwnerBehavior() };
        wizard.PetOwnerBehavior.SetEnergy(11);

        Assert.Equal(expected, Evaluate(new ReqEnergy { m_isPercent = isPercent, m_numericValue = required, m_operatorType = OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ }, wizard));
    }

    [Theory]
    [InlineData(40, true)]
    [InlineData(41, false)]
    public void ReqHealthPercent_ComparesPercentOfMaxHealth(float required, bool expected) {
        var wizard = new Wizard { GameStats = new ServerWizGameStats(MagicSchool.Fire, 1) { m_currentHitpoints = 200, m_baseHitpoints = 500 } };

        Assert.Equal(expected, Evaluate(new ReqHealthPercent { m_numericValue = required, m_operatorType = OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ }, wizard));
    }

    [Theory]
    [InlineData(25, true)]
    [InlineData(24, false)]
    public void ReqManaPercent_ComparesPercentOfMaxMana(float required, bool expected) {
        var wizard = new Wizard { GameStats = new ServerWizGameStats(MagicSchool.Fire, 1) { m_currentMana = 10, m_baseMana = 40 } };

        Assert.Equal(expected, Evaluate(new ReqManaPercent { m_numericValue = required, m_operatorType = OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ }, wizard));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void ReqGardeningLevel_ComparesGardeningLevel(float required, bool expected) {
        var wizard = new Wizard { GameStats = new ServerWizGameStats(MagicSchool.Fire, 1) { m_gardeningLevel = 3 } };

        Assert.Equal(expected, Evaluate(new ReqGardeningLevel { m_numericValue = required, m_operatorType = OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ }, wizard));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ReqInventoryFull_ComparesFreeBackpackSlots(bool fillBackpack, bool expected) {
        var wizard = new Wizard { InventoryBehavior = new ServerWizInventoryBehavior { Items = [] } };
        while (fillBackpack && wizard.InventoryBehavior.FreeSlots > 0) {
            wizard.InventoryBehavior.Items.Add(new WizClientObjectItem());
        }

        Assert.Equal(expected, Evaluate(new ReqInventoryFull { m_numericValue = 1, m_operatorType = OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ }, wizard));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ReqInCombat_FollowsDuelState(bool inDuel, bool expected) {
        var wizard = new Wizard { IsInDuel = inDuel };

        Assert.Equal(expected, Evaluate(new ReqInCombat(), wizard));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ReqPvPCombat_HoldsInAPvPMatchDuel(bool inMatch, bool expected) {
        const ulong charId = 913_001;
        var wizard = new Wizard { CharId = charId, IsInDuel = true };
        PvPRegistry.SetInMatch(charId, inMatch);
        try {
            Assert.Equal(expected, Evaluate(new ReqPvPCombat(), wizard));
        } finally {
            PvPRegistry.SetInMatch(charId, false);
        }
    }

    [Fact]
    public void ReqPipCounts_FailWithoutADuelState() {
        var wizard = new Wizard { IsInDuel = true };

        Assert.False(Evaluate(new ReqPipCount { m_minPips = 0, m_maxPips = 14 }, wizard));
        Assert.False(Evaluate(new ReqShadowPipCount { m_minPips = 0, m_maxPips = 14 }, wizard));
    }

    [Theory]
    [InlineData("Quest", true)]
    [InlineData("Other", false)]
    public void ReqIsQuest_MatchesTheQuestBeingChecked(string questName, bool expected) {
        var requirement = new ReqIsQuest { m_questName = questName };

        Assert.Equal(expected, Evaluate(requirement, new Wizard(), "Quest"));
    }

    [Theory]
    [InlineData("Quest", "Goal", true)]
    [InlineData("Quest", "Other", false)]
    [InlineData("Other", "Goal", false)]
    public void ReqIsGoal_MatchesTheGoalBeingChecked(string questName, string goalName, bool expected) {
        var requirement = new ReqIsGoal { m_questName = questName, m_goalName = goalName };

        Assert.Equal(expected, Evaluate(requirement, new Wizard(), "Quest", "Goal"));
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void ReqHighestCharacterLevelOnAccount_ComparesTheAccountsHighestLevel(float required, bool expected) {
        var wizard = WizardAtLevel(5);
        OnOneAccount(wizard, WizardAtLevel(30), WizardAtLevel(12));

        Assert.Equal(expected, Evaluate(new ReqHighestCharacterLevelOnAccount { m_numericValue = required, m_operatorType = OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ }, wizard));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void ReqOtherCharacterQuestCompleted_LooksOnlyAtOtherCharacters(bool otherCompleted, bool selfCompleted, bool expected) {
        var wizard = new Wizard { CharId = 913_010, QuestBehavior = new ServerQuestBehavior() };
        var other = new Wizard { CharId = 913_011, QuestBehavior = new ServerQuestBehavior() };
        if (otherCompleted) {
            other.QuestBehavior.AddToQuestRegistry("Quest", "Complete", 1);
        }

        if (selfCompleted) {
            wizard.QuestBehavior.AddToQuestRegistry("Quest", "Complete", 1);
        }

        OnOneAccount(wizard, other);

        Assert.Equal(expected, Evaluate(new ReqOtherCharacterQuestCompleted { m_quest = "Quest" }, wizard));
    }

    [Theory]
    [InlineData("Fire Cat", true, true)]
    [InlineData("Fire Cat", false, true)]
    [InlineData("FireCat", true, false)]
    [InlineData("FireCat", false, true)]
    [InlineData("Frost Beetle", false, false)]
    public void ReqHasSpell_MatchesLearnedSpellByNameOrBase(string spellName, bool exact, bool expected) {
        var wizard = WizardWithSpell(913_101, new SpellTemplate { m_name = "Fire Cat", m_spellBase = "FireCat", m_sMagicSchoolName = "Fire" }, MagicSchool.Ice);

        Assert.Equal(expected, Evaluate(new ReqHasSpell { m_spellName = spellName, m_hasExactSpellName = exact }, wizard));
    }

    [Theory]
    [InlineData("Fire", true)]
    [InlineData("Ice", false)]
    [InlineData("Life", false)]
    public void ReqHasSecondarySchool_HoldsForASchoolTrainedOutsideThePrimary(string school, bool expected) {
        var wizard = WizardWithSpell(913_102, new SpellTemplate { m_name = "Fire Cat", m_sMagicSchoolName = "Fire" }, MagicSchool.Ice);
        wizard.SpellbookBehavior.LearnedSpellTemplateIds.Add(913_103);
        SeedTemplate(913_103, new SpellTemplate { m_name = "Frost Beetle", m_sMagicSchoolName = "Ice" });

        Assert.Equal(expected, Evaluate(new ReqHasSecondarySchool { m_secondarySchool = school }, wizard));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ReqClassProjectActive_NoClassProjectIsActive(bool applyNot, bool expected) {
        var requirement = new ReqClassProjectActive { m_classProjectName = "BattleGround_Event_01", m_applyNOT = applyNot };

        Assert.Equal(expected, Evaluate(requirement, new Wizard()));
    }

    private static bool Evaluate(Requirement requirement, Wizard wizard, string? questName = null, string? goalName = null) {
        var requirements = new RequirementList { m_requirements = [requirement] };

        return RequirementDispatcher.EvaluateRequirements(requirements, new GenericRequirementContext(requirements, null, null, wizard, questName: questName, goalName: goalName));
    }

    private static Wizard WizardAtLevel(int level)
        => new() { MagicSchoolBehavior = new ServerMagicSchoolBehavior { Level = level } };

    private static void OnOneAccount(params Wizard[] wizards) {
        var account = (Account) RuntimeHelpers.GetUninitializedObject(typeof(Account));
        account.Characters = [.. wizards];
        foreach (var wizard in wizards) {
            wizard.Account = account;
        }
    }

    private static Wizard WizardWithSpell(uint templateId, SpellTemplate spell, MagicSchool primarySchool) {
        SeedTemplate(templateId, spell);
        var wizard = new Wizard {
            MagicSchoolBehavior = new ServerMagicSchoolBehavior { MagicSchool = primarySchool },
            SpellbookBehavior = new ServerWizSpellbookBehavior(),
        };
        wizard.SpellbookBehavior.LearnedSpellTemplateIds.Add(templateId);

        return wizard;
    }

    private static void SeedTemplate(ulong templateId, CoreTemplate template) {
        var cache = (ConcurrentDictionary<ulong, CoreTemplate>) typeof(CoreObjectFactory)
            .GetField("s_templateCache", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        cache[templateId] = template;
    }

    private static RequirementList HasEntry(string entryName)
        => new() { m_requirements = [new ReqHasEntry { m_entryName = entryName }] };

}
