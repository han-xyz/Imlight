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
 * LEVEL UP ELIXIR TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the backpack space a level-up or world elixir needs
 * for the items and gear it gives the player's school, and the quest-start
 * message for the quests it adds.
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
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Elixirs;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using Xunit;

namespace Imlight.Tests;

public sealed class LevelUpElixirTests {

    private static readonly LevelUpElixirBehaviorTemplate s_elixir = new() {
        m_allSchoolData = new LevelUpElixirSchoolSpecificData { m_itemsToPlaceInInventory = [11, 0, 12], m_gearToEquip = [] },
        m_schoolSpecificData = [
            new LevelUpElixirSchoolSpecificData { m_schoolName = "Fire", m_itemsToPlaceInInventory = [], m_gearToEquip = [21] },
            new LevelUpElixirSchoolSpecificData { m_schoolName = "Ice", m_itemsToPlaceInInventory = [31], m_gearToEquip = [32, 33] },
        ],
    };

    [Fact]
    public void InventorySlotsNeeded_CountsAllSchoolAndOwnSchoolItems() {
        Assert.Equal(3, LevelUpElixirHelper.InventorySlotsNeeded(s_elixir, "Fire"));
        Assert.Equal(5, LevelUpElixirHelper.InventorySlotsNeeded(s_elixir, "ice"));
    }

    [Fact]
    public void InventorySlotsNeeded_SchoolWithoutData_CountsAllSchoolOnly() {
        Assert.Equal(2, LevelUpElixirHelper.InventorySlotsNeeded(s_elixir, "Storm"));
    }

    [Fact]
    public void QuestStartMessage_ShowsStoredRewards() {
        var template = new QuestTemplate { m_questName = "Elixir Entry", m_goals = [], m_startGoals = [] };
        var started = QuestService.StartQuestInstance(template, 1,
            _ => new DropTableResult { Items = [new DropItemResult { ItemId = "4242", Quantity = 1 }] },
            _ => true);

        var message = LevelUpElixirHelper.BuildQuestStartingMessage(started, template, new Wizard { CharId = 1 });

        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);
        Assert.True(serializer.Deserialize<LootInfoList>((byte[]) message.Rewards, 1u, out var loot));
        Assert.NotNull(loot);
        var item = Assert.IsType<ItemLootInfo>(Assert.Single(loot.m_loot));
        Assert.Equal(4242UL, (ulong) item.m_itemID);
    }

}
