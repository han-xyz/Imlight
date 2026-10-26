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
 * The game data tests read the extracted client root wad (ROOTWAD_DIR) and the data pack (SPIRALDB_CLONE).
 * ShardReagent_Granted stops short of the database write in Wizard.AddReagent.
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
using System.IO;
using System.Linq;
using System.Text.Json;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Game.Reagents;
using Imlight.CoreLib.Game.TieredSpells;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class TieredSpellTests {

    private const string SpellementDropTable = "droptables_spellements_p_school_arc01_01.json";
    private const string ShardPrefix = "Shard-";
    private const int RealGroupCount = 283;

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

    [GameDataFact]
    public void TieredSpellIndex_LoadsRealGroups() {
        var groups = TieredSpellIndex.Read(
            File.ReadAllBytes(Path.Combine(s_rootWadDirectory!, "TieredSpells.xml")),
            File.ReadAllBytes(Path.Combine(s_rootWadDirectory!, "TieredSpellsGroupInfo.xml")));

        Assert.Equal(RealGroupCount, groups.Count);
        Assert.All(groups, group => {
            Assert.NotEqual(0UL, group.ShardTemplateId);
            Assert.Contains(group.TierOneSpell, group.Spells);
        });
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
        InitializeQuietLogging();
        RootArchiveLoader.UseRootWad(ArchiveOf(s_rootWadDirectory!, ["TemplateManifest.xml", "ObjectData/Reagents/", "Locale/en-US/"]));
        _ = CoreObjectFactory.Instance;
        _ = ReagentFactory.Instance;
        var reagentBag = new ServerAlchemyBehavior();

        foreach (var templateId in SpellementDropIds()) {
            var template = Assert.IsAssignableFrom<ReagentItemTemplate>(CoreObjectFactory.GetCoreTemplate(templateId));
            var name = template.m_objectName.ToString();
            Assert.StartsWith(ShardPrefix, name);
            Assert.Equal(templateId, (ulong) ReagentFactory.GetReagentTemplate(name).m_templateID);

            var shard = Assert.IsType<ClientReagentItem>(CoreObjectFactory.FinalizeCoreObject(templateId));
            Assert.True(reagentBag.AddReagent(shard));
            Assert.True(reagentBag.HasReageant(templateId));
        }
    }

    private static HashSet<ulong> SpellementDropIds() {
        using var dropTable = JsonDocument.Parse(File.ReadAllText(Path.Combine(s_spiralDbClone!, "DropTables", SpellementDropTable)));

        return dropTable.RootElement.GetProperty("Items").EnumerateArray()
            .Select(item => ulong.Parse(item.GetProperty("ItemId").GetString()!, CultureInfo.InvariantCulture))
            .ToHashSet();
    }

    private static Archive ArchiveOf(string directory, string[] pathPrefixes) {
        var stream = new MemoryStream();
        var files = new Dictionary<string, FileEntry>();
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
            var name = Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/');
            if (!pathPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) {
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            files[name] = new FileEntry {
                Offset = (uint) stream.Position,
                UncompressedSize = (uint) bytes.Length,
                CompressedSize = (uint) bytes.Length,
                FileName = name,
            };
            stream.Write(bytes);
        }

        return new Archive(files, stream, version: 1);
    }

    private static void InitializeQuietLogging() {
        var iniFilePath = Path.GetTempFileName();
        var logPath = Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt");
        try {
            File.WriteAllText(iniFilePath, $"[Logging]\nLogPath = {logPath}\nLogLevel = ERROR\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
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
