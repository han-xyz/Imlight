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
 * Behaviour tests for the server-side quest offer gate: a requirement-free chain
 * quest waits for its predecessor.
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

public sealed class QuestOfferGateTests {

    private static readonly string[] s_pack = [
        "WC-HUB-C01-001", "WC-HUB-C01-002", "WC-HUB-C01-004", "WC-HUB-C01-005", "MB-SIDE-001", "MB-SIDE-002",
        "WC-COMMONS-MAIN-001", "WC-COMMONS-MAIN-002-FIRE", "WC-OLDE-MAIN-001", "WC-OLDE-MAIN-002B",
        "WC-OLDE-MAIN-003A", "Opt-Lonely-Quest",
    ];

    private static QuestOfferGate Gate(bool requirePredecessor = true)
        => QuestOfferGate.FromPack(s_pack.Select(name => new QuestTemplate { m_questName = name }).ToList(),
            requirePredecessor);

    private static bool NothingCompleted(string questName) => false;

    [Theory]
    [InlineData("WC-HUB-C01-002", "WC-HUB-C01-001")]
    [InlineData("WC-HUB-C01-005", "WC-HUB-C01-004")]
    [InlineData("MB-SIDE-002", "MB-SIDE-001")]
    public void Predecessor_NotCompleted_Locked(string questName, string predecessor) {
        var quest = new QuestTemplate { m_questName = questName };

        Assert.False(Gate().IsUnlocked(quest, NothingCompleted));
        Assert.True(Gate().IsUnlocked(quest, name => name == predecessor));
    }

    [Theory]
    [InlineData("WC-HUB-C01-001")]
    [InlineData("Opt-Lonely-Quest")]
    [InlineData("WC-COMMONS-MAIN-002-FIRE")]
    [InlineData("WC-OLDE-MAIN-002B")]
    [InlineData("WC-OLDE-MAIN-003A")]
    public void Predecessor_FirstOfChainOrNotAChainName_Unlocked(string questName) {
        Assert.True(Gate().IsUnlocked(new QuestTemplate { m_questName = questName }, NothingCompleted));
    }

    [Fact]
    public void Predecessor_MissingFromPack_Unlocked() {
        Assert.True(Gate().IsUnlocked(new QuestTemplate { m_questName = "WC-HUB-C01-004" }, NothingCompleted));
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
