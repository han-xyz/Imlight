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
 * Prep-speaker scan finds candidates and this gate filters them.
 *
 * USAGE EXAMPLE:
 * var gate = QuestOfferGate.FromPack(QuestTemplateCollection.GetAllQuests(), matchWorld: true);
 * gate.IsOfferedBy(quest.m_questName, npcPersona);
 *
 * NOTE:
 * A world is the quest name's token before the first '-'. Only prefixes shared by
 * at least five pack quests count as worlds; anything else is left ungated.
 *
 * TODO:
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class QuestOfferGate {

    private const int KNOWN_WORLD_MIN_QUESTS = 5;
    private const char WORLD_SEPARATOR = '-';

    private readonly bool _matchWorld;
    private readonly IReadOnlySet<string> _knownWorlds;

    private QuestOfferGate(bool matchWorld, IReadOnlySet<string> knownWorlds) {
        _matchWorld = matchWorld;
        _knownWorlds = knownWorlds;
    }

    public static QuestOfferGate FromPack(IReadOnlyCollection<QuestTemplate> quests, bool matchWorld)
        => new(matchWorld, KnownWorlds(quests.Select(quest => (string) quest.m_questName)));

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

    private static string WorldOf(string name)
        => name?.Split(WORLD_SEPARATOR, 2)[0] ?? "";

}
