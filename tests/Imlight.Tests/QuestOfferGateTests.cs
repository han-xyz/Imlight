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
 * QUEST OFFER GATE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the server-side quest offer gate: an NPC offers only
 * quests of its own world, and a requirement-free chain quest waits for its predecessor.
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

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Components;
using Xunit;

namespace Imlight.Tests;

public class QuestOfferGateTests {

    private static readonly string[] s_pack = [
        "WC-HUB-C01-001", "WC-HUB-C01-002", "WC-ST01-C01-001", "WC-ST02-C01-001", "WC-ST02-C01-002",
        "KT-HUB-C05-001", "KT-HUB-C05-002", "KT-HUB-C05-003", "KT-HUB-C09-001", "KT-HUB-C09-002",
        "Opt-Lonely-Quest", "MB-SIDE-001", "MB-SIDE-002",
    ];

    private static QuestOfferGate Gate(bool matchWorld = true, bool requirePredecessor = true)
        => QuestOfferGate.FromPack(s_pack.Select(name => new QuestTemplate { m_questName = name }).ToList(),
            matchWorld, requirePredecessor);

    private static bool NothingCompleted(string questName) => false;

    [Fact]
    public void KnownWorlds_PrefixesWithFiveQuests() {
        Assert.Equal(["KT", "WC"], QuestOfferGate.KnownWorlds(s_pack).Order());
    }

    [Fact]
    public void WorldMatch_OtherWorldQuest_NotOfferedByNpc() {
        Assert.False(Gate().IsOfferedBy("KT-HUB-C05-003", "WC-HUB-NPC03_Persona"));
        Assert.True(Gate().IsOfferedBy("WC-HUB-C01-002", "WC-HUB-NPC03_Persona"));
    }

    [Fact]
    public void WorldMatch_UnknownPrefixOnEitherSide_RuleDoesNotApply() {
        Assert.True(Gate().IsOfferedBy("Opt-Lonely-Quest", "WC-HUB-NPC03_Persona"));
        Assert.True(Gate().IsOfferedBy("KT-HUB-C05-003", "WC_TritonAvenue_Vendor_SandorSpitfire"));
        Assert.True(Gate().IsOfferedBy("KT-HUB-C05-003", null));
    }

    [Fact]
    public void WorldMatch_Disabled_OffersEveryWorld() {
        Assert.True(Gate(matchWorld: false).IsOfferedBy("KT-HUB-C05-003", "WC-HUB-NPC03_Persona"));
    }

    [Fact]
    public void Predecessor_NotCompleted_Locked() {
        var quest = new QuestTemplate { m_questName = "WC-HUB-C01-002" };

        Assert.False(Gate().IsUnlocked(quest, NothingCompleted));
        Assert.True(Gate().IsUnlocked(quest, name => name == "WC-HUB-C01-001"));
    }

    [Fact]
    public void Predecessor_SideChain_Locked() {
        var quest = new QuestTemplate { m_questName = "MB-SIDE-002" };

        Assert.False(Gate().IsUnlocked(quest, NothingCompleted));
        Assert.True(Gate().IsUnlocked(quest, name => name == "MB-SIDE-001"));
    }

    [Fact]
    public void Predecessor_FirstOfChainOrUnindexed_Unlocked() {
        Assert.True(Gate().IsUnlocked(new QuestTemplate { m_questName = "WC-HUB-C01-001" }, NothingCompleted));
        Assert.True(Gate().IsUnlocked(new QuestTemplate { m_questName = "Opt-Lonely-Quest" }, NothingCompleted));
    }

    [Fact]
    public void Predecessor_MissingFromPack_Unlocked() {
        Assert.True(Gate().IsUnlocked(new QuestTemplate { m_questName = "KT-HUB-C07-002" }, NothingCompleted));
    }

    [Fact]
    public void Predecessor_QuestWithRequirements_Untouched() {
        var quest = new QuestTemplate { m_questName = "WC-HUB-C01-002", m_prepRequirements = new RequirementList() };

        Assert.True(Gate().IsUnlocked(quest, NothingCompleted));
    }

    [Fact]
    public void Predecessor_Disabled_Unlocked() {
        var quest = new QuestTemplate { m_questName = "WC-HUB-C01-002" };

        Assert.True(Gate(requirePredecessor: false).IsUnlocked(quest, NothingCompleted));
    }

}
