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
 * STARTER KIT
 * ========================================================================
 * 
 * PURPOSE:
 * The items, pet and equipped slots a new character starts with: the QA kit for QualityAssurance and above, the
 * retail kit of the character's school for every other account.
 * 
 * USAGE EXAMPLE:
 * var kit = StarterKits.Instance.For(wizard.Account.AuthLevel, wizard.MagicSchoolBehavior.MagicSchool);
 * 
 * NOTE:
 * Read from the [Character] section: QaDefaultItems (template ids; DefaultItems is its deprecated alias),
 * RetailStarterItems.<School> and StarterPet (template names, resolved against the TemplateManifest at boot).
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
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Inventory;

internal sealed record StarterKit(
    IReadOnlyList<uint> ItemTemplateIds,
    uint PetTemplateId,
    IReadOnlyList<EquipmentSlotType> EquippedSlots);

internal sealed class StarterKits {

    private const string QaItemsKey = "QaDefaultItems";
    private const string DeprecatedQaItemsKey = "DefaultItems";
    private const string RetailItemsKeyPrefix = "RetailStarterItems.";
    private const string StarterPetKey = "StarterPet";
    private const uint QaPetTemplateId = 126412; // Black Cat

    private static readonly MagicSchool[] s_primarySchools = [
        MagicSchool.Fire, MagicSchool.Ice, MagicSchool.Storm, MagicSchool.Myth,
        MagicSchool.Life, MagicSchool.Death, MagicSchool.Balance,
    ];

    private static readonly EquipmentSlotType[] s_qaEquippedSlots = [EquipmentSlotType.Weapon, EquipmentSlotType.Deck];

    private static readonly EquipmentSlotType[] s_retailEquippedSlots = [
        EquipmentSlotType.Hat, EquipmentSlotType.Robe, EquipmentSlotType.Shoes,
        EquipmentSlotType.Weapon, EquipmentSlotType.Deck,
    ];

    private static StarterKits s_instance;

    private readonly StarterKit _qaKit;
    private readonly Dictionary<MagicSchool, StarterKit> _retailKits;
    private readonly StarterKit _emptyRetailKit;

    private StarterKits(StarterKit qaKit, Dictionary<MagicSchool, StarterKit> retailKits, StarterKit emptyRetailKit) {
        _qaKit = qaKit;
        _retailKits = retailKits;
        _emptyRetailKit = emptyRetailKit;
    }

    /// <summary>
    /// The kits of the loaded configuration and TemplateManifest, resolved on first use.
    /// </summary>
    internal static StarterKits Instance
        => LazyInitializer.EnsureInitialized(ref s_instance, () => Load(ConfigurationManager.GetSection("Character"),
            TemplateIdsByName(CoreObjectFactory.TemplateManifest)));

    /// <summary>
    /// Resolves the kits from the [Character] settings; a retail template name the manifest lacks is logged and skipped.
    /// </summary>
    internal static StarterKits Load(IReadOnlyDictionary<string, string> characterSettings,
                                     IReadOnlyDictionary<string, uint> templateIdsByName) {
        var qaKit = new StarterKit(QaItemTemplateIds(characterSettings), QaPetTemplateId, s_qaEquippedSlots);

        var petNames = Names(characterSettings, StarterPetKey);
        var retailPetId = ResolveNames(petNames, templateIdsByName).FirstOrDefault();
        var retailKits = s_primarySchools.ToDictionary(school => school, school => new StarterKit(
            ResolveNames(Names(characterSettings, RetailItemsKeyPrefix + school), templateIdsByName),
            retailPetId,
            s_retailEquippedSlots));

        return new StarterKits(qaKit, retailKits, new StarterKit([], retailPetId, s_retailEquippedSlots));
    }

    /// <summary>
    /// Indexes the manifest's templates by file name without extension, e.g. Hat-T1-001; the first entry wins.
    /// </summary>
    internal static IReadOnlyDictionary<string, uint> TemplateIdsByName(TemplateManifest manifest) {
        var idsByName = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var location in manifest.m_serializedTemplates) {
            if (location is not null) {
                idsByName.TryAdd(Path.GetFileNameWithoutExtension(location.m_filename.ToString()), location.m_id);
            }
        }

        return idsByName;
    }

    internal StarterKit For(AuthLevel authLevel, MagicSchool school) {
        if (authLevel >= AuthLevel.QualityAssurance) {
            return _qaKit;
        }

        return _retailKits.GetValueOrDefault(school, _emptyRetailKit);
    }

    private static uint[] QaItemTemplateIds(IReadOnlyDictionary<string, string> characterSettings) {
        var key = QaItemsKey;
        if (!characterSettings.ContainsKey(QaItemsKey) && characterSettings.ContainsKey(DeprecatedQaItemsKey)) {
            Logger.Warning("[Character] DefaultItems is deprecated; rename it to QaDefaultItems.");
            key = DeprecatedQaItemsKey;
        }

        return Names(characterSettings, key)
            .Select(id => uint.TryParse(id, out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .ToArray();
    }

    private static string[] Names(IReadOnlyDictionary<string, string> characterSettings, string key)
        => new ConfigurationManager.ConfigValue(characterSettings.GetValueOrDefault(key, "")).AsList();

    private static uint[] ResolveNames(IEnumerable<string> names, IReadOnlyDictionary<string, uint> templateIdsByName) {
        var ids = new List<uint>();
        foreach (var name in names) {
            if (templateIdsByName.TryGetValue(name, out var id)) {
                ids.Add(id);
            } else {
                Logger.Warning("Starter kit template {Name} is not in the TemplateManifest; it is skipped.",
                    Logger.Args(name));
            }
        }

        return [.. ids];
    }

}
