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
 * STARTER KIT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the starter kit a new character receives: the QA kit for QualityAssurance and above, the
 * school's retail kit for every other account.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * The shipped-config tests read the extracted client root wad (ROOTWAD_DIR) and Imlight.Director/Config/Imlight.ini.
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
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Game.Inventory;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class StarterKitTests {

    private const uint BlackCatTemplateId = 126412;

    private static readonly string? s_rootWadDirectory = Environment.GetEnvironmentVariable("ROOTWAD_DIR");

    private static readonly MagicSchool[] s_primarySchools = [
        MagicSchool.Fire, MagicSchool.Ice, MagicSchool.Storm, MagicSchool.Myth,
        MagicSchool.Life, MagicSchool.Death, MagicSchool.Balance,
    ];

    private static readonly Dictionary<string, uint> s_templateIdsByName = new() {
        ["Hat-Fire"] = 1,
        ["Deck-Fire"] = 2,
        ["Hat-Ice"] = 3,
        ["Pet-Owl"] = 4,
    };

    [Theory]
    [InlineData(AuthLevel.QualityAssurance)]
    [InlineData(AuthLevel.HallMonitor)]
    [InlineData(AuthLevel.Developer)]
    [InlineData(AuthLevel.Administrator)]
    public void StaffAccount_GetsQaKit(AuthLevel authLevel) {
        var kits = StarterKits.Load(Settings(), s_templateIdsByName);

        var kit = kits.For(authLevel, MagicSchool.Fire);

        Assert.Equal([10u, 11u], kit.ItemTemplateIds);
        Assert.Equal(BlackCatTemplateId, kit.PetTemplateId);
        Assert.Equal([EquipmentSlotType.Weapon, EquipmentSlotType.Deck], kit.EquippedSlots);
    }

    [Fact]
    public void NormalAccount_GetsSchoolRetailKit() {
        var kits = StarterKits.Load(Settings(), s_templateIdsByName);

        var fire = kits.For(AuthLevel.None, MagicSchool.Fire);
        var ice = kits.For(AuthLevel.None, MagicSchool.Ice);

        Assert.Equal([1u, 2u], fire.ItemTemplateIds);
        Assert.Equal([3u], ice.ItemTemplateIds);
        Assert.Equal(0u, fire.PetTemplateId);
        Assert.Equal([EquipmentSlotType.Hat, EquipmentSlotType.Robe, EquipmentSlotType.Shoes, EquipmentSlotType.Weapon,
            EquipmentSlotType.Deck], fire.EquippedSlots);
    }

    [Fact]
    public void MissingAccount_GetsSchoolRetailKit() {
        var kits = StarterKits.Load(Settings(), s_templateIdsByName);

        Assert.Equal([1u, 2u], kits.For(null, MagicSchool.Fire).ItemTemplateIds);
    }

    [Fact]
    public void RetailKit_SkipsUnknownTemplateName() {
        var kits = StarterKits.Load(Settings(("RetailStarterItems.Fire", "Hat-Fire, Robe-Missing")), s_templateIdsByName);

        Assert.Equal([1u], kits.For(AuthLevel.None, MagicSchool.Fire).ItemTemplateIds);
    }

    [Fact]
    public void RetailKit_GrantsConfiguredStarterPet() {
        var kits = StarterKits.Load(Settings(("StarterPet", "Pet-Owl")), s_templateIdsByName);

        Assert.Equal(4u, kits.For(AuthLevel.None, MagicSchool.Fire).PetTemplateId);
    }

    [Fact]
    public void QaKit_FallsBackToDeprecatedDefaultItems() {
        var settings = Settings(("DefaultItems", "7"));
        settings.Remove("QaDefaultItems");

        var kits = StarterKits.Load(settings, s_templateIdsByName);

        Assert.Equal([7u], kits.For(AuthLevel.QualityAssurance, MagicSchool.Fire).ItemTemplateIds);
    }

    [RootWadFact]
    public void ShippedRetailKits_ResolveWithoutMountOrCrownItem() {
        var kits = StarterKits.Load(ShippedCharacterSettings(), TemplateIdsByName());
        var filenamesById = LoadedTemplateFilenames();

        foreach (var school in s_primarySchools) {
            var kit = kits.For(AuthLevel.None, school);

            Assert.Equal(2, kit.ItemTemplateIds.Count);
            Assert.Equal(0u, kit.PetTemplateId);
            Assert.All(kit.ItemTemplateIds, id => {
                Assert.DoesNotContain("Crown", filenamesById[id], StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Mount", filenamesById[id], StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotMatch("Hat-|Robe-|Shoe-", filenamesById[id]);
            });
        }
    }

    [RootWadFact]
    public void ShippedQaKit_IsUnchanged() {
        var kits = StarterKits.Load(ShippedCharacterSettings(), TemplateIdsByName());

        var kit = kits.For(AuthLevel.QualityAssurance, MagicSchool.Balance);

        Assert.Equal([1317127u, 1317128u, 1317125u, 1317124u, 1317126u, 1317129u, 1317130u, 1317131u, 1317132u, 1317133u,
            1317234u, 87256u, 284071u, 126983u], kit.ItemTemplateIds);
        Assert.Equal(BlackCatTemplateId, kit.PetTemplateId);
    }

    private static Dictionary<string, string> Settings(params (string Key, string Value)[] overrides) {
        var settings = new Dictionary<string, string> {
            ["QaDefaultItems"] = "10, 11",
            ["RetailStarterItems.Fire"] = "Hat-Fire, Deck-Fire",
            ["RetailStarterItems.Ice"] = "Hat-Ice",
            ["StarterPet"] = "",
        };
        foreach (var (key, value) in overrides) {
            settings[key] = value;
        }

        return settings;
    }

    private static Dictionary<string, string> ShippedCharacterSettings() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "src", "Imlight.Director", "Config", "Imlight.ini"))) {
            directory = directory.Parent;
        }

        ConfigurationManager.Initialize(Path.Combine(directory.FullName, "src", "Imlight.Director", "Config", "Imlight.ini"));
        var settings = ConfigurationManager.GetSection("Character");
        InitializeQuietLogging();

        return settings;
    }

    private static IReadOnlyDictionary<string, uint> TemplateIdsByName() {
        RootArchiveLoader.UseRootWad(ArchiveOf(s_rootWadDirectory!, ["TemplateManifest.xml"]));
        _ = CoreObjectFactory.Instance;

        return StarterKits.TemplateIdsByName(CoreObjectFactory.TemplateManifest);
    }

    private static Dictionary<uint, string> LoadedTemplateFilenames()
        => CoreObjectFactory.TemplateManifest.m_serializedTemplates
            .Where(location => location is not null)
            .GroupBy(location => location.m_id)
            .ToDictionary(group => group.Key, group => group.First().m_filename.ToString());

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

    private sealed class RootWadFactAttribute : FactAttribute {
        public RootWadFactAttribute() {
            if (!Directory.Exists(s_rootWadDirectory)) {
                Skip = "set ROOTWAD_DIR to the extracted client root wad to run this test";
            }
        }
    }

}
