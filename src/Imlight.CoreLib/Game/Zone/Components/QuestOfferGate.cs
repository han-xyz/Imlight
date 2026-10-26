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
 * Server-side rules that narrow the quests an NPC offers. The client data carries
 * no per-NPC offer list (every WizardQuestingBehavior m_questList is empty), so the
 * Prep-speaker scan finds candidates and this gate filters them: by world, and a
 * requirement-free chain quest waits for the quest before it.
 *
 * USAGE EXAMPLE:
 * var gate = QuestOfferGate.FromPack(QuestTemplateCollection.GetAllQuests(), matchWorld: true, requirePredecessor: true);
 * gate.IsOfferedBy(quest.m_questName, npcPersona) && gate.IsUnlocked(quest, wizard.HasCompletedQuest);
 *
 * NOTE:
 * A world is the quest name's token before the first '-'. Only prefixes shared by
 * at least five pack quests count as worlds; anything else is left ungated.
 * A chain quest is named "<world>-<area>-C<nn>-<kkk>" or "...-SIDE-<kkk>"/"...-MAIN-<kkk>";
 * its predecessor is the same name with kkk - 1.
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

    private const int KNOWN_WORLD_MIN_QUESTS = 5;
    private const char WORLD_SEPARATOR = '-';

    public static readonly QuestOfferGate Open = new(false, false, new HashSet<string>(), new HashSet<string>());

    private readonly bool _matchWorld;
    private readonly bool _requirePredecessor;
    private readonly IReadOnlySet<string> _knownWorlds;
    private readonly IReadOnlySet<string> _packQuestNames;

    private QuestOfferGate(bool matchWorld, bool requirePredecessor, IReadOnlySet<string> knownWorlds,
                           IReadOnlySet<string> packQuestNames) {
        _matchWorld = matchWorld;
        _requirePredecessor = requirePredecessor;
        _knownWorlds = knownWorlds;
        _packQuestNames = packQuestNames;
    }

    [GeneratedRegex(@"^(?<stem>.+-(?:C\d+|SIDE|MAIN)-)(?<index>\d+)$")]
    private static partial Regex ChainQuestName();

    public static QuestOfferGate FromPack(IReadOnlyCollection<QuestTemplate> quests, bool matchWorld, bool requirePredecessor) {
        var questNames = quests.Select(quest => (string) quest.m_questName).ToHashSet(StringComparer.Ordinal);

        return new(matchWorld, requirePredecessor, KnownWorlds(questNames), questNames);
    }

    public static IReadOnlySet<string> KnownWorlds(IEnumerable<string> questNames)
        => questNames
            .GroupBy(WorldOf, StringComparer.Ordinal)
            .Where(world => world.Count() >= KNOWN_WORLD_MIN_QUESTS)
            .Select(world => world.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// False when the quest and the NPC's persona belong to different known worlds.
    /// </summary>
    public bool IsOfferedBy(string questName, string npcPersona) {
        if (!_matchWorld) {
            return true;
        }

        var questWorld = WorldOf(questName);
        var npcWorld = WorldOf(npcPersona);
        if (!_knownWorlds.Contains(questWorld) || !_knownWorlds.Contains(npcWorld)) {
            return true;
        }

        return questWorld == npcWorld;
    }

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

    private static string WorldOf(string name)
        => name?.Split(WORLD_SEPARATOR, 2)[0] ?? "";

}
