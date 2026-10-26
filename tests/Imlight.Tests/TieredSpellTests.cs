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
 * TIERED SPELL TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the spellement groups: the index read from the game data, the visibility a wizard
 * gets and the shard reagents the spellement drop tables grant.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * ShardReagent_Granted reads the extracted client root wad (ROOTWAD_DIR) and the data pack (SPIRALDB_CLONE).
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Game.TieredSpells;
using Xunit;

namespace Imlight.Tests;

public sealed class TieredSpellTests {

    private const string SpellementDropTable = "droptables_spellements_p_school_arc01_01.json";
    private const string ShardPrefix = "Shard-";

    private static readonly string? s_rootWadDirectory = Environment.GetEnvironmentVariable("ROOTWAD_DIR");
    private static readonly string? s_spiralDbClone = Environment.GetEnvironmentVariable("SPIRALDB_CLONE");

    private static readonly IndexedTieredSpellGroup s_fireCat = new("Fire Cat", 1492248, ["Fire Cat", "Fire Cat - T02 - A"]);
    private static readonly IndexedTieredSpellGroup s_frostBeetle = new("Frost Beetle", 1494687, ["Frost Beetle"]);
    private static readonly IndexedTieredSpellGroup s_scarab = new("Scarab", 1494700, ["Scarab", "Scarab - T02 - A"]);

    [Fact]
    public void TieredSpellIndex_Loads_GroupsWithShards() {
        var config = new TieredSpellConfig {
            m_tieredSpellGroupList = [
                new TieredSpellGroup { m_tierOneSpell = "Fire Cat", m_shardType = new GID(1492248) },
                new TieredSpellGroup { m_tierOneSpell = "Frost Beetle", m_shardType = new GID(1494687) },
            ]
        };
        var groupInfo = new TieredSpellGroupInfoList {
            m_tieredSpellGroupInfoList = [
                GroupInfo("Fire Cat", 0, "Fire Cat"),
                GroupInfo("Frost Beetle", 1, "Frost Beetle"),
                GroupInfo("Fire Cat - T02 - A", 0, "Fire Cat"),
            ]
        };

        var groups = TieredSpellIndex.Read(ToBind(config), ToBind(groupInfo));

        Assert.Equal(["Fire Cat", "Frost Beetle"], groups.Select(g => g.TierOneSpell));
        Assert.Equal([1492248UL, 1494687UL], groups.Select(g => g.ShardTemplateId));
        Assert.Equal(["Fire Cat", "Fire Cat - T02 - A"], groups[0].Spells);
        Assert.Equal(["Frost Beetle"], groups[1].Spells);
    }

    [Fact]
    public void Visibility_TrainedOrOwned_IsVisible() {
        var visibility = TieredSpellVisibility.For(
            [s_fireCat, s_frostBeetle, s_scarab],
            trainedSpells: new HashSet<string> { "Fire Cat - T02 - A" },
            ownsReagent: templateId => templateId == s_frostBeetle.ShardTemplateId);

        Assert.Equal([1, 1, 0], visibility.m_visibilityList);
        Assert.Equal([0, 0, 0], visibility.m_canUpgradeList);
        Assert.Equal([0, 0, 0], visibility.m_doPrepTieredSpellForMPList);
    }

    [Fact]
    public void Visibility_Serialises() {
        var visibility = TieredSpellVisibility.For([s_fireCat, s_scarab], trainedSpells: new HashSet<string> { "Scarab" }, ownsReagent: _ => false);

        var data = TieredSpellVisibility.Serialize(visibility);

        Assert.True(new ObjectSerializer(Versionable: false)
            .Deserialize(data, PropertyFlags.Prop_Public, out TieredSpellGroupVisibility? decoded));
        Assert.Equal([0, 1], decoded!.m_visibilityList);
        Assert.Equal([0, 0], decoded.m_canUpgradeList);
        Assert.Equal([0, 0], decoded.m_doPrepTieredSpellForMPList);
    }

    [GameDataFact]
    public void ShardReagent_Granted() {
        var dropTable = JsonDocument.Parse(File.ReadAllText(Path.Combine(s_spiralDbClone!, "DropTables", SpellementDropTable)));
        var droppedIds = dropTable.RootElement.GetProperty("Items").EnumerateArray()
            .Select(item => ulong.Parse(item.GetProperty("ItemId").GetString()!))
            .ToHashSet();
        var shards = Directory.GetFiles(Path.Combine(s_rootWadDirectory!, "ObjectData", "Reagents", "Shards"))
            .Select(path => new BindSerializer().Deserialize<ReagentItemTemplate>(File.ReadAllBytes(path), out var template)
                ? template
                : null)
            .ToList();

        Assert.All(shards, shard => Assert.StartsWith(ShardPrefix, shard?.m_objectName.ToString()));
        Assert.Subset(shards.Select(shard => (ulong) shard!.m_templateID).ToHashSet(), droppedIds);
    }

    private static TieredSpellGroupInfo GroupInfo(string spellName, int groupIndex, string tierOneSpell)
        => new() {
            m_spellName = spellName,
            m_theTieredSpellGroupInfoData = new TieredSpellGroupInfoData {
                m_tsGroupIndex = groupIndex,
                m_tsGroupTierOneSpellName = tierOneSpell,
            }
        };

    private static byte[] ToBind(PropertyClass value) {
        var serializer = new ObjectSerializer(Versionable: true, BindSerializer.BiNDDefaultFlags);
        Assert.True(serializer.Serialize(value, PropertyFlags.Prop_Save, out var body));

        return [.. BitConverter.GetBytes(BindSerializer.BiNDMagic), .. (byte[]) body];
    }

    private sealed class GameDataFactAttribute : FactAttribute {
        public GameDataFactAttribute() {
            if (!Directory.Exists(s_rootWadDirectory) || !Directory.Exists(s_spiralDbClone)) {
                Skip = "set ROOTWAD_DIR to the extracted client root wad and SPIRALDB_CLONE to a spiraldb clone to run this test";
            }
        }
    }

}
