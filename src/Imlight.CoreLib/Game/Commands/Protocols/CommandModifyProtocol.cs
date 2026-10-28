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

using System.Globalization;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

/// <summary>
/// Commands that change the issuer's own character. The admin panel's character tabs run the same actions
/// (<see cref="CharacterActions"/>) on any character.
/// </summary>
internal class CommandModifyProtocol : CommandProtocol {

    internal override string Group { get; set; } = "mod";

    [Command("levelup")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("lvlup")]
    private void LevelUpCommand() => Inform(CharacterActions.LevelUp(OwnCharacter));

    [Command("level")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetLevelCommand(string level) => Inform(CharacterActions.SetLevel(OwnCharacter, level));

    [Command("speed")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetSpeedCommand(string speedBonus) => Inform(CharacterActions.SetSpeed(OwnCharacter, speedBonus));

    [Command("additem")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddItemCommand(string templateId) => Inform(CharacterActions.AddItem(OwnCharacter, templateId));

    [Command("addsnack")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddSnackCommand(string templateId) => Inform(CharacterActions.AddSnack(OwnCharacter, templateId));

    [Command("petlevel")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetPetLevelCommand(string level) => Inform(CharacterActions.SetPetLevel(OwnCharacter, level));

    [Command("petxp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddPetXpCommand(string xp) => Inform(CharacterActions.AddPetExperience(OwnCharacter, xp));

    [Command("pethappiness")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetPetHappinessCommand(string happiness)
        => Inform(CharacterActions.SetPetHappiness(OwnCharacter, happiness));

    [Command("pettalent")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void GivePetTalentCommand(string talentName)
        => Inform(CharacterActions.GivePetTalent(OwnCharacter, talentName));

    [Command("addreagent")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddReagentCommand(string templateId) => Inform(CharacterActions.AddReagent(OwnCharacter, templateId));

    [Command("addreagents")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddReagentsCommand(string templateId, string count)
        => Inform(CharacterActions.AddReagents(OwnCharacter, templateId, count));

    [Command("name")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetNameCommand([Remainder] string name) => Inform(CharacterActions.SetNameOverride(OwnCharacter, name));

    [Command("badge")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetBadgeCommand([Remainder] string badge) => Inform(CharacterActions.SetBadgeTitle(OwnCharacter, badge));

    [Command("maxgold")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxGoldCommand(string gold) => Inform(CharacterActions.SetMaxGold(OwnCharacter, gold));

    [Command("maxbank")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxBankCommand(string limit) => Inform(CharacterActions.SetBankLimit(OwnCharacter, limit));

    [Command("expandbank")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ExpandBankCommand() => Inform(CharacterActions.ExpandBank(OwnCharacter));

    [Command("addgold")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddGoldCommand(string gold) => Inform(CharacterActions.AddGold(OwnCharacter, gold));

    [Command("maxhealth")]
    [Alias("maxhp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxHealthCommand(string health) => Inform(CharacterActions.SetMaxHealth(OwnCharacter, health));

    [Command("maxmana")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxManaCommand(string mana) => Inform(CharacterActions.SetMaxMana(OwnCharacter, mana));

    [Command("maxenergy")]
    [Alias("maxnrg")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetMaxEnergyCommand(string energy) => Inform(CharacterActions.SetMaxEnergy(OwnCharacter, energy));

    [Command("currenthealth")]
    [Alias("currenthp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCurrentHealthCommand(string health) => Inform(CharacterActions.SetHealth(OwnCharacter, health));

    [Command("currentmana")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCurrentManaCommand(string mana) => Inform(CharacterActions.SetMana(OwnCharacter, mana));

    [Command("currentenergy")]
    [Alias("currentnrg")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCurrentEnergyCommand(string energy) => Inform(CharacterActions.SetEnergy(OwnCharacter, energy));

    [Command("refillhealth")]
    [Alias("refillhp", "heal")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void MaxHealthCommand() => Inform(CharacterActions.RefillHealth(OwnCharacter));

    [Command("refillmana")]
    [Alias("refillmp", "rejuvenate", "rejuv")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void MaxManaCommand() => Inform(CharacterActions.RefillMana(OwnCharacter));

    [Command("refillenergy")]
    [Alias("refillen", "refillnrg", "refillpet", "energize")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void MaxEnergyCommand() => Inform(CharacterActions.RefillEnergy(OwnCharacter));

    [Command("cantriplevelup")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("clvlup")]
    private void CantripLevelUpCommand() => Inform(CharacterActions.CantripLevelUp(OwnCharacter));

    [Command("cantriplevel")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [Alias("clvl")]
    private void SetCantripLevelCommand(string level) => Inform(CharacterActions.SetCantripLevel(OwnCharacter, level));

    [Command("addtrainingpoints")]
    [Alias("addtp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddTrainingPointsCommand(string trainingPoints)
        => Inform(CharacterActions.AddTrainingPoints(OwnCharacter, trainingPoints));

    [Command("settrainingpoints")]
    [Alias("settp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetTrainingPointsCommand(string trainingPoints)
        => Inform(CharacterActions.SetTrainingPoints(OwnCharacter, trainingPoints));

    [Command("potionmax")]
    [Alias("pmax")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetPotionMax(string potionMax) => Inform(CharacterActions.SetPotionMax(OwnCharacter, potionMax));

    [Command("addxp")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddXPCommand(string xp) => Inform(CharacterActions.AddExperience(OwnCharacter, xp));

    [Command("setcrowns")]
    [Alias("crowns")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetCrownsCommand(string crowns) {
        if (!int.TryParse(crowns, NumberStyles.Integer, CultureInfo.InvariantCulture, out var crownsAmount) || crownsAmount < 0) {
            InformSenderClient("Invalid Crowns amount.");

            return;
        }

        if (!Context.Account.SetCrowns(crownsAmount)) {
            InformSenderClient("The Crowns could not be saved.");

            return;
        }

        // CacheBalanceForCSSegmentation Updates the internal Crown Shop cache so the client uses this balance
        // for Crown Shop segment checks and affordability calculation
        var msg = new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = 0,
            TotalCrowns = crownsAmount,
            CharacterID = Context.CharacterObject.m_globalID,
            CacheBalanceForCSSegmentation = 1
        };

        Context.SessionActor.Tell(msg);
        InformSenderClient($"Set Crowns to {crownsAmount}.");
    }

}
