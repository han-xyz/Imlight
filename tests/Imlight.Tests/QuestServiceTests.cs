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
 * from the data pack, and for caching quest offers.
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
 * Last Updated: 10/25/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class QuestServiceTests {

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

}
