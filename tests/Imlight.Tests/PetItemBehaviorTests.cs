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
 * PET ITEM BEHAVIOR TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the order of a pet item's behaviors: the client pairs them with its template's behavior slots by index.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * The tests read the extracted client root wad (ROOTWAD_DIR).
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
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Shared.Resources;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class PetItemBehaviorTests {

    private const uint ZappyGlowbugTemplateId = 1596313;
    private const ulong OwnerId = 5581269150249111495;

    private static readonly string? s_rootWadDirectory = Environment.GetEnvironmentVariable("ROOTWAD_DIR");

    private static readonly System.Type[] s_glowbugSlotTypes = [
        typeof(ClientPetNameBehavior),
        typeof(FurnitureInfoBehavior),
        typeof(ClientPetItemBehavior),
        typeof(ClientJewelSocketBehavior),
    ];

    [RootWadFact]
    public void HatchedPet_BehaviorsFillTemplateSlots() {
        LoadGameData();

        var pet = PetFactory.CreateHatchedPet(OwnerId, ZappyGlowbugTemplateId);

        Assert.Equal(s_glowbugSlotTypes, pet.m_inactiveBehaviors.Select(behavior => behavior?.GetType()));
    }

    [RootWadFact]
    public void StoredPet_BehaviorsMoveToTemplateSlots() {
        LoadGameData();
        var petItem = new ClientPetItemBehavior { m_level = 3 };
        var petName = new ClientPetNameBehavior { m_nameKeys = 42 };
        var pet = new WizClientObjectItem {
            m_templateID = ZappyGlowbugTemplateId,
            m_inactiveBehaviors = [petItem, petName],
        };

        CoreObjectFactory.ArrangeBehaviorsByTemplate(pet);

        Assert.Equal(s_glowbugSlotTypes, pet.m_inactiveBehaviors.Select(behavior => behavior?.GetType()));
        Assert.Same(petName, pet.m_inactiveBehaviors[0]);
        Assert.Same(petItem, pet.m_inactiveBehaviors[2]);
    }

    [RootWadFact]
    public void ArrangeBehaviorsByTemplate_IsIdempotent() {
        LoadGameData();
        var pet = new WizClientObjectItem {
            m_templateID = ZappyGlowbugTemplateId,
            m_inactiveBehaviors = [new ClientPetItemBehavior { m_level = 3 }, new ClientPetNameBehavior { m_nameKeys = 42 }],
        };

        CoreObjectFactory.ArrangeBehaviorsByTemplate(pet);
        var arranged = pet.m_inactiveBehaviors.ToList();
        CoreObjectFactory.ArrangeBehaviorsByTemplate(pet);

        Assert.Equal(arranged.Count, pet.m_inactiveBehaviors.Count);
        for (var slot = 0; slot < arranged.Count; slot++) {
            Assert.Same(arranged[slot], pet.m_inactiveBehaviors[slot]);
        }
    }

    [RootWadFact]
    public void ArrangeBehaviorsByTemplate_KeepsBehaviorWithoutSlot() {
        LoadGameData();
        var extraName = new ClientPetNameBehavior { m_nameKeys = 7 };
        var pet = new WizClientObjectItem {
            m_templateID = ZappyGlowbugTemplateId,
            m_inactiveBehaviors = [new ClientPetNameBehavior { m_nameKeys = 42 }, extraName],
        };

        CoreObjectFactory.ArrangeBehaviorsByTemplate(pet);

        Assert.Contains(extraName, pet.m_inactiveBehaviors);
    }

    private static void LoadGameData() {
        InitializeQuietLogging();
        RootArchiveLoader.UseRootWad(ArchiveOf(s_rootWadDirectory!, ["TemplateManifest.xml"]));
        _ = CoreObjectFactory.Instance;
        var templateFile = CoreObjectFactory.TemplateManifest.m_serializedTemplates
            .First(location => location is not null && location.m_id == ZappyGlowbugTemplateId).m_filename.ToString();

        RootArchiveLoader.UseRootWad(ArchiveOf(s_rootWadDirectory!, ["TemplateManifest.xml", "ObjectData/Pets/", templateFile]));
        _ = PetFactory.Instance;
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

    private sealed class RootWadFactAttribute : FactAttribute {
        public RootWadFactAttribute() {
            if (!Directory.Exists(s_rootWadDirectory)) {
                Skip = "set ROOTWAD_DIR to the extracted client root wad to run this test";
            }
        }
    }

}
