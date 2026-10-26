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
 * QUEST OFFER GATE
 * ========================================================================
 *
 * PURPOSE:
 * Server-side rule that narrows the quests an NPC offers. The client data carries
 * no per-NPC offer list (every WizardQuestingBehavior m_questList is empty), so the
 * Prep-speaker scan finds candidates and this gate holds back a requirement-free
 * chain quest until the quest before it is complete.
 *
 * USAGE EXAMPLE:
 * var gate = QuestOfferGate.FromPack(QuestTemplateCollection.GetAllQuests(), requirePredecessor: true);
 * gate.IsUnlocked(quest, wizard.HasCompletedQuest);
 *
 * NOTE:
 * A chain quest is named "<world>-<area>-C<nn>-<kkk>" or "...-SIDE-<kkk>"/"...-MAIN-<kkk>";
 * its predecessor is the same name with kkk - 1. Names with a suffix after the index
 * (-MAIN-002-FIRE, -MAIN-002B) are not chain quests here.
 *
 * TODO:
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed partial class QuestOfferGate {

    public static readonly QuestOfferGate Open = new(false, new HashSet<string>());

    private readonly bool _requirePredecessor;
    private readonly IReadOnlySet<string> _packQuestNames;

    private QuestOfferGate(bool requirePredecessor, IReadOnlySet<string> packQuestNames) {
        _requirePredecessor = requirePredecessor;
        _packQuestNames = packQuestNames;
    }

    [GeneratedRegex(@"^(?<stem>.+-(?:C\d+|SIDE|MAIN)-)(?<index>\d+)$")]
    private static partial Regex ChainQuestName();

    public static QuestOfferGate FromPack(IReadOnlyCollection<QuestTemplate> quests, bool requirePredecessor)
        => new(requirePredecessor, quests.Select(quest => (string) quest.m_questName).ToHashSet(StringComparer.Ordinal));

    /// <summary>
    /// False when the quest has no requirements and its chain predecessor, present in the pack, is not completed.
    /// </summary>
    public bool IsUnlocked(QuestTemplate quest, Func<string, bool> isCompleted) {
        if (!_requirePredecessor || HasRequirements(quest)) {
            return true;
        }

        var predecessor = PredecessorOf(quest.m_questName);

        return predecessor is null || !_packQuestNames.Contains(predecessor) || isCompleted(predecessor);
    }

    private static bool HasRequirements(QuestTemplate quest)
        => quest.m_requirements is not null || quest.m_prepRequirements is not null || quest.m_pruneRequirements is not null;

    private static string PredecessorOf(string questName) {
        var match = ChainQuestName().Match(questName ?? "");
        if (!match.Success) {
            return null;
        }

        var digits = match.Groups["index"].Value;
        var index = int.Parse(digits, CultureInfo.InvariantCulture);
        if (index <= 1) {
            return null;
        }

        return match.Groups["stem"].Value + (index - 1).ToString(new string('0', digits.Length), CultureInfo.InvariantCulture);
    }

}
