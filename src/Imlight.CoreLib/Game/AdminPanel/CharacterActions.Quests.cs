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
 */

using System.Globalization;
using System.Linq;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <remarks>
/// Quests, badges and PvP ratings are run by the character's own session services, through the same paths as
/// play, so these need the character online. The services answer by themselves: the player for a chat command,
/// the staff member for the admin panel (<see cref="CharacterTarget.Replies"/>).
/// </remarks>
internal static partial class CharacterActions {

    #region Quests

    /// <summary>
    /// Runs one of the quest service's actions: remove, completegoal, complete, grant, forcegrant, list or info.
    /// An empty quest name means the character's only active quest.
    /// </summary>
    internal static AdminActionResult RunQuestCommand(CharacterTarget target, string action, string questName) {
        target.Tell(new CHARACTER_103_PROTOCOL.MSG_QUESTCOMMAND {
            Action = action,
            QuestName = questName?.Trim() ?? "",
            ReplyTo = target.Replies?.ReplyTo,
            RequestId = target.Replies?.RequestId ?? 0
        });

        return AdminActionResult.Sent();
    }

    /// <summary>
    /// Shows the quest's offer dialog, as an NPC would. Accepting the offer grants the quest.
    /// </summary>
    internal static AdminActionResult OfferQuest(CharacterTarget target, string questName) {
        questName = questName?.Trim() ?? "";
        var quest = QuestTemplateCollection.GetQuestByName(questName);
        if (quest is null) {
            return Refuse($"Quest '{questName}' does not exist.");
        }

        if (target.Wizard.HasQuest(questName)) {
            return Refuse($"The character already has the quest '{questName}'.");
        }

        ShowQuestInfoDialog(target, quest);
        SendQuestOfferDialog(target, quest);
        target.Tell(new CHARACTER_103_PROTOCOL.MSG_SENDQUESTOFFERCACHEOPTION { Quest = quest });

        return Done($"Offered the quest '{questName}'.");
    }

    private static void ShowQuestInfoDialog(CharacterTarget target, QuestTemplate quest) {
        var dialogList = quest.m_dialogList as ActorDialogList;
        var prepDialog = dialogList?.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");
        if (prepDialog is null) {
            return;
        }

        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(DialogSpeakers.ForClient(prepDialog), 16, out var serializedData)) {
            Logger.Error("Failed to serialize '{0}' dialog.", Logger.Args("QuestInfo"));

            return;
        }

        target.Tell(new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = 0, // No specific NPC for command-based dialogs
            QuestID = 0,
            GoalID = 0,
            CompletionType = "QuestInfo",
            ActorDialog = serializedData,
            Persona = "",
            PersonaName = "",
            PersonaIcon = "",
        });
        target.Tell(new CHARACTER_103_PROTOCOL.MSG_DIALOGSHOWN());
    }

    private static void SendQuestOfferDialog(CharacterTarget target, QuestTemplate quest) {
        var startingGoals = quest.m_goals
            .Where(g => quest.m_startGoals.Contains(g.m_goalName))
            .ToList();

        var startingGoalCompilation = new GoalCompilation {
            m_goals = [.. startingGoals.Select(goal => new GoalEntryFull {
                m_personaName = "",
                m_goalType = (int) goal.m_goalType,
                m_goalCount = 0,
                m_goalTotal = goal.m_tallyCounter?.m_count ?? 0,
                m_useTally = goal.m_tallyCounter is not null,
                m_tallyText = goal.m_tallyCounter?.m_descriptor ?? string.Empty,
                m_tallyText2 = goal.m_tallyCounter?.m_descriptor2 ?? string.Empty,
                m_goalTitle = goal.m_goalTitle,
                m_goalLocation = goal.m_locationName,
                m_goalDestinationZone = goal.m_destinationZone,
                m_goalImage1 = goal.m_displayImage1,
                m_goalImage2 = goal.m_displayImage2,
                m_goalNameID = goal.m_goalNameID,
                m_goalMadlibs = QuestMadlibs.GetAppropriateMadlibBlockForGoal(goal, null)
            })]
        };

        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(startingGoalCompilation, 1, out var serializedGoals)) {
            Logger.Error("Failed to serialize starting goals for quest {0}.", Logger.Args(quest.m_questName));

            return;
        }

        var rewards = GetQuestRewards(target, quest);
        if (!serializer.Serialize(rewards, 1, out var serializedRewards)) {
            Logger.Error("Failed to serialize rewards for quest {0}.", Logger.Args(quest.m_questName));

            return;
        }

        target.Tell(new QUEST_MESSAGES_52_PROTOCOL.MSG_QUESTOFFER {
            MobileID = 0, // No specific NPC for command-based offers
            QuestName = quest.m_questName,
            QuestTitle = quest.m_questTitle,
            QuestInfo = "",
            Level = quest.m_questLevel,
            Rewards = serializedRewards,
            GoalData = serializedGoals,
            Mainline = (byte) (quest.m_mainline ? 1 : 0),
        });
    }

    private static LootInfoList GetQuestRewards(CharacterTarget target, QuestTemplate quest) {
        // Quest rewards are listed in drop tables by "ResDropTable" in the completion results.
        if (quest?.m_endResults?.m_results is null) {
            return new LootInfoList();
        }

        var dropTableNames = quest.m_endResults.m_results
            .OfType<ResDropTable>()
            .Select(x => x.m_tableName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        // "Roll" the drop tables to get the actual items.
        var rollResult = DropTableRoller.Roll(dropTableNames, target.Session, target.GameObject, target.Wizard);

        return DropTableConverter.ToLootInfoList(rollResult);
    }

    #endregion

    #region Badges and PvP ratings

    /// <summary>
    /// Runs one of the badge service's actions: grant (a badge id or name), progress (a counter key and an
    /// amount), list or reset.
    /// </summary>
    internal static AdminActionResult RunBadgeCommand(CharacterTarget target, string action, string argument,
                                                      string amount = null) {
        ulong parsedAmount = 0;
        if (amount is not null && !ulong.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out parsedAmount)) {
            return Refuse("Invalid amount.");
        }

        target.Tell(new CHARACTER_103_PROTOCOL.MSG_BADGECOMMAND {
            Action = action,
            Argument = argument?.Trim() ?? "",
            Amount = parsedAmount,
            ReplyTo = target.Replies?.ReplyTo,
            RequestId = target.Replies?.RequestId ?? 0
        });

        return AdminActionResult.Sent();
    }

    /// <summary>
    /// Runs one of the rating service's actions: show, set (a rating), rankset (a rank index or name),
    /// seasonreset or clear. An empty league means the first rated league the character may use.
    /// </summary>
    internal static AdminActionResult RunRatingCommand(CharacterTarget target, string action, string value,
                                                       string league) {
        target.Tell(new PVP_111_PROTOCOL.MSG_RATINGCOMMAND {
            Action = action,
            Value = value?.Trim() ?? "",
            League = league?.Trim() ?? "",
            ReplyTo = target.Replies?.ReplyTo,
            RequestId = target.Replies?.RequestId ?? 0
        });

        return AdminActionResult.Sent();
    }

    #endregion

}
