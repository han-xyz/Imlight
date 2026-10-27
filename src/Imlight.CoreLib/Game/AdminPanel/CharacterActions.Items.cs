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
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Bank;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Game.Pet.Hatching;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.AdminPanel;

internal static partial class CharacterActions {

    private const int ParentsPerEgg = 2;

    #region Items

    internal static AdminActionResult AddItem(CharacterTarget target, string templateId) {
        if (!ulong.TryParse(templateId, out var templateIdLong)) {
            return Refuse("Invalid item id.");
        }

        var template = CoreObjectFactory.GetCoreTemplate(templateIdLong);
        if (template is null) {
            return Refuse("Invalid item id.");
        }

        // Game objects cannot go in the inventory.
        if (template is not WizItemTemplate) {
            return Refuse($"Cannot add objects of type {template.GetType().Name} to inventory.");
        }

        var isPetTemplate = PetFactory.IsPetTemplate((uint) templateIdLong);
        if (!isPetTemplate && template.m_behaviors.Any(b => b?.m_behaviorName.ToString() == "PetItemBehavior")) {
            return Refuse("Cannot add this pet item because it is not in the pet list.");
        }

        if (!isPetTemplate) {
            var probe = CoreObjectFactory.FinalizeCoreObject(templateIdLong) as WizClientObjectItem;
            if (probe is not null) {
                CoreObjectFactory.InitializeCoreObjectBehaviors(probe, probe.m_templateID);
            }

            if (probe?.m_inactiveBehaviors?.Contains(null) == true) {
                return Refuse("Cannot add this item because one of its behaviors cannot be created.");
            }
        }

        var wizard = target.Wizard;
        if (!wizard.TryCreateItem(templateIdLong, out var item)) {
            return Refuse("Could not create the item.");
        }

        // Like a reward: the backpack first, then the bank.
        var placement = ItemPlacement.Place(wizard, item);
        ItemPlacement.Announce(target.Session, wizard, item, placement);

        return placement switch {
            Placement.Backpack => Done($"Added item {item.m_debugName} to inventory."),
            Placement.Bank => Done($"Added item {item.m_debugName} to the bank, the inventory is full."),
            Placement.SharedBank => Done($"Added item {item.m_debugName} to the shared bank, the inventory is full."),
            _ => Refuse("Could not add the item, the inventory and the bank are full."),
        };
    }

    internal static AdminActionResult AddSnack(CharacterTarget target, string templateId) {
        if (!ulong.TryParse(templateId, out var templateIdLong)) {
            return Refuse("Invalid item id.");
        }

        var template = CoreObjectFactory.GetCoreTemplate(templateIdLong);
        if (template is null) {
            return Refuse("Invalid item id.");
        }

        if (template is not PetSnackItemTemplate) {
            return Refuse($"Cannot add objects of type {template.GetType().Name} to snack bag.");
        }

        var wizard = target.Wizard;
        if (!wizard.AddSnack(templateIdLong, out var snack)) {
            return Refuse("Could not add snack to snack bag.");
        }

        var serializer = new CoreObjectSerializer(behaviors: SerializerFlags.None);
        if (!serializer.Serialize(snack, 24, out var serializedSnack)) {
            return Refuse("Failed to serialize core object.");
        }

        target.Tell(new PET_9_PROTOCOL.MSG_PETSNACKADD { GlobalID = wizard.GameObjectID, Data = serializedSnack });
        target.Tell(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = snack.m_globalID,
            ItemTemplateID = (uint) snack.m_templateID,
            ItemLocation = 1
        });
        target.Tell(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE {
            GlobalID = wizard.GameObjectID,
            ItemID = snack.m_globalID,
            Quantity = snack.m_quantity
        });

        return Done($"Added snack {snack.m_debugName} to snack bag.");
    }

    internal static AdminActionResult AddReagent(CharacterTarget target, string templateId) {
        if (!ulong.TryParse(templateId, out var templateIdLong)) {
            return Refuse("Invalid item id.");
        }

        var template = CoreObjectFactory.GetCoreTemplate(templateIdLong);
        if (template is null) {
            return Refuse("Invalid item id.");
        }

        if (template is not ReagentItemTemplate) {
            return Refuse($"Cannot add objects of type {template.GetType().Name} to reagent bag.");
        }

        var wizard = target.Wizard;
        if (!wizard.AddReagent(templateIdLong, out var reagent)) {
            return Refuse("Could not add reagent to reagent bag.");
        }

        var serializer = new CoreObjectSerializer(behaviors: SerializerFlags.None);
        if (!serializer.Serialize(reagent, 27, out var serializedReagent)) {
            return Refuse("Failed to serialize core object.");
        }

        target.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTADD { GlobalID = wizard.GameObjectID, Data = serializedReagent });
        target.Tell(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = reagent.m_globalID,
            ItemTemplateID = (uint) reagent.m_templateID,
            ItemLocation = 1
        });
        target.Tell(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE {
            GlobalID = wizard.GameObjectID,
            ItemID = reagent.m_globalID,
            Quantity = reagent.m_quantity
        });

        return Done($"Added reagent {reagent.m_debugName} to reagent bag.");
    }

    internal static AdminActionResult AddReagents(CharacterTarget target, string templateId, string count) {
        if (!ulong.TryParse(templateId, out var templateIdLong)
            || CoreObjectFactory.GetCoreTemplate(templateIdLong) is not ReagentItemTemplate) {
            return Refuse("Invalid reagent id.");
        }

        if (!int.TryParse(count, out var countInt) || countInt <= 0) {
            return Refuse("Invalid count.");
        }

        var wizard = target.Wizard;
        var held = wizard.AlchemyBehavior.GetReagent(templateIdLong)?.m_quantity ?? 0;
        var granted = Math.Min(countInt, ServerAlchemyBehavior.MaxReagentStack - held);
        if (granted <= 0 || !LootGranter.GrantReagents(target.Session, wizard, templateIdLong, granted)) {
            return Refuse("The reagent stack is full.");
        }

        return Done($"Added {granted} of {countInt} reagents {templateIdLong} to reagent bag.");
    }

    internal static AdminActionResult GrantHouse(CharacterTarget target, string templateId) {
        if (!uint.TryParse(templateId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || !HouseTemplates.TryGet(id, out var template)) {
            return Refuse($"Template {templateId} is not a house.");
        }

        var wizard = target.Wizard;
        var grant = HouseDeeds.Issue(wizard, id);
        foreach (var item in grant.Created) {
            ItemSync.SendAdded(target.Session, wizard, item);
        }

        if (!grant.Succeeded) {
            return Refuse(grant.Failure == HouseGrantFailure.BackpackFull
                ? "The backpack is too full for that house."
                : $"Could not give house {id}.");
        }

        return Done(grant.Created.Count == 0
            ? $"Already has house {id}, zone {template.LotZone}."
            : $"Gave {grant.Created.Count} house item(s) for zone {template.LotZone}.");
    }

    #endregion

    #region Bank

    internal static AdminActionResult SetBankLimit(CharacterTarget target, string limit) {
        if (!int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limitInt) || limitInt < 1) {
            return Refuse("Invalid bank limit.");
        }

        if (!target.Wizard.SetBankLimit(limitInt)) {
            return Refuse("Could not save the bank limit.");
        }

        target.Tell(BankSync.UpdateBankLimit(target.Wizard));

        return Done($"Set the bank limit to {limitInt}.");
    }

    internal static AdminActionResult ExpandBank(CharacterTarget target) {
        var amount = BankConfig.ExpandElixirAmount;
        if (amount <= 0) {
            return Refuse("[Bank] ExpandElixirAmount is not set, so the bank cannot be expanded.");
        }

        var wizard = target.Wizard;
        var before = wizard.StorageBehavior.Limit;
        if (!wizard.ExpandBank(amount)) {
            return Refuse("Could not save the bank limit.");
        }

        target.Tell(BankSync.UpdateBankLimit(wizard));

        return Done($"Expanded the bank from {before} to {wizard.StorageBehavior.Limit}.");
    }

    #endregion

    #region Pets

    internal static AdminActionResult SetPetLevel(CharacterTarget target, string level) {
        if (!int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var petLevel)) {
            return Refuse("Invalid pet level.");
        }

        if (!TryGetEquippedPet(target, out var pet, out var refusal)) {
            return refusal;
        }

        if (!PetProgression.SetLevel(pet, petLevel)) {
            return Refuse($"Cannot set the pet to level {petLevel}.");
        }

        SendPetBehavior(target, pet);

        return Done($"Set the pet to level {petLevel}.");
    }

    // Pet XP can earn loot, whose requirements are checked against the player object, so it needs the
    // character online.
    internal static AdminActionResult AddPetExperience(CharacterTarget target, string xp) {
        if (!uint.TryParse(xp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount == 0) {
            return Refuse("Invalid XP amount.");
        }

        if (!TryGetEquippedPet(target, out var pet, out var refusal)) {
            return refusal;
        }

        var result = PetXpAward.Give(target.Session, target.GameObject, target.Wizard, pet, amount, PetXpSource.Command);
        if (!result.Applied) {
            return Refuse("This pet cannot gain XP.");
        }

        return Done($"Gave the pet {result.Granted} XP and {result.LevelsGained} levels.");
    }

    internal static AdminActionResult SetPetHappiness(CharacterTarget target, string happiness) {
        if (!uint.TryParse(happiness, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) {
            return Refuse("Invalid happiness.");
        }

        if (!TryGetEquippedPet(target, out var pet, out var refusal)) {
            return refusal;
        }

        if (!PetProgression.SetHappiness(pet, value)) {
            return Refuse("This pet cannot have its happiness set.");
        }

        SendPetBehavior(target, pet);

        return Done($"Set the pet's happiness to {value}, up to its maximum.");
    }

    internal static AdminActionResult GivePetTalent(CharacterTarget target, string talentName) {
        if (!PetTalentRegistry.TryGetByName(talentName, out var talent)) {
            return Refuse($"No pet talent is named {talentName}.");
        }

        if (!TryGetEquippedPet(target, out var pet, out var refusal)) {
            return refusal;
        }

        if (!PetProgression.GiveTalent(pet, talent)) {
            return Refuse("This pet cannot be given a talent.");
        }

        SendPetBehavior(target, pet);

        return Done($"Gave the pet the talent {talent.Name}.");
    }

    private static bool TryGetEquippedPet(CharacterTarget target, out WizClientObjectItem pet,
                                          out AdminActionResult refusal) {
        pet = target.Wizard.GetEquippedPetItem();
        refusal = pet is null ? Refuse("Equip a pet first.") : null;

        return pet is not null;
    }

    private static void SendPetBehavior(CharacterTarget target, WizClientObjectItem pet) {
        target.Wizard.SavePet(pet);

        if (PetBehaviorUpdate.TryCreate(pet, out var update)) {
            target.Tell(update);
        }
    }

    #endregion

    #region Hatching

    /// <summary>
    /// Lists the pets that can be parents, or lays the egg of two of them. A pet is named by its number in the
    /// list, its global id or its name without spaces. No gold is taken.
    /// </summary>
    internal static AdminActionResult Hatch(CharacterTarget target, string parents) {
        var candidates = PetHatching.GetParentCandidates(target.Wizard);
        var names = (parents ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0) {
            return ListParents(candidates);
        }

        if (names.Length != ParentsPerEgg) {
            return Refuse("Name two pets: hatch (pet) (pet). Type hatch alone to list them.");
        }

        if (!TryFindParent(candidates, names[0], out var first, out var refusal)
            || !TryFindParent(candidates, names[1], out var second, out refusal)) {
            return refusal;
        }

        return LayEgg(target, first, second);
    }

    private static AdminActionResult LayEgg(CharacterTarget target, WizClientObjectItem first,
                                            WizClientObjectItem second) {
        if (first.m_globalID == second.m_globalID) {
            return Refuse("Pick two different pets.");
        }

        if (!target.Wizard.PetOwnerBehavior.HasFreeMorphingSlot()) {
            return Refuse("The last egg has not hatched yet.");
        }

        if (!PetHatching.TryLayEgg(target.Session, target.Wizard, first, second, out var egg)) {
            return Refuse("The egg could not be made.");
        }

        var minutes = Math.Ceiling(TimeSpan.FromSeconds(PetHatching.GetSecondsToHatch(egg)).TotalMinutes);

        return Done($"{GetPetName(first)} and {GetPetName(second)} made {GetPetName(egg)}, which hatches in {minutes} minutes.");
    }

    private static AdminActionResult ListParents(List<WizClientObjectItem> candidates) {
        if (candidates.Count == 0) {
            return Done("None of the pets is old enough to hatch.");
        }

        var lines = new List<string>();
        for (var i = 0; i < candidates.Count; i++) {
            var pet = candidates[i];
            PetProgression.TryGetPet(pet, out var behavior, out _);
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{i + 1}. {GetPetName(pet)}, level {behavior?.m_level}, id {pet.m_globalID.Full}"));
        }

        return AdminActionResult.Done("Pets that can hatch:", lines);
    }

    private static bool TryFindParent(List<WizClientObjectItem> candidates, string name, out WizClientObjectItem pet,
                                      out AdminActionResult refusal) {
        pet = FindByNumber(candidates, name)
            ?? candidates.FirstOrDefault(candidate => candidate.m_globalID.Full.ToString(CultureInfo.InvariantCulture) == name)
            ?? candidates.FirstOrDefault(candidate => string.Equals(GetPetName(candidate).Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase));
        refusal = pet is null ? Refuse($"There is no pet {name} that can hatch.") : null;

        return pet is not null;
    }

    private static WizClientObjectItem FindByNumber(List<WizClientObjectItem> candidates, string name)
        => int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= candidates.Count
            ? candidates[number - 1]
            : null;

    private static string GetPetName(WizClientObjectItem pet)
        => CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(pet, out var name)
            ? WizardNameBank.GetPetEnglishName(name.m_nameKeys)
            : pet.m_globalID.Full.ToString(CultureInfo.InvariantCulture);

    #endregion

}
