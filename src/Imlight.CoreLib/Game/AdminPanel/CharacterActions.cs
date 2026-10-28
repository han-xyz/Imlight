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
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// The character actions shared by the chat commands and the admin panel: each changes one character, live or
/// offline (see <see cref="CharacterTarget"/>), and says what it did.
/// </summary>
/// <remarks>
/// The chat commands call these with the issuer's own character. The admin panel calls them with any character,
/// and checks the staff member's level and, for an action that needs the player online, that the character is.
/// Messages say what happened to the character, never "you", since the reader may be staff acting on someone
/// else's character.
/// </remarks>
internal static partial class CharacterActions {

    // StringHash of "SpeedBuff" which can be found in Root.wad/GameEffectData/CanonicalStatEffect.xml
    private const uint SpeedEffectName = 6543894;

    private static AdminActionResult Refuse(string message) => AdminActionResult.Refuse(message);

    private static AdminActionResult Done(string message) => AdminActionResult.Done(message);

    #region Level and experience

    internal static AdminActionResult LevelUp(CharacterTarget target)
        => SetLevel(target, target.Wizard.MagicSchoolBehavior.Level + 1);

    internal static AdminActionResult SetLevel(CharacterTarget target, string level)
        => byte.TryParse(level, out var levelByte) ? SetLevel(target, levelByte) : Refuse("Invalid level.");

    private static AdminActionResult SetLevel(CharacterTarget target, int level) {
        if (level < 1) {
            return Refuse("Invalid level.");
        }

        if (level > MagicLevelsConfig.MaxLevel) {
            return Refuse($"You cannot set level higher than the max level ({MagicLevelsConfig.MaxLevel}).");
        }

        // A live character's wizard service sets the level and tells the player and the zone.
        if (target.Online) {
            target.Tell(new CHARACTER_103_PROTOCOL.MSG_LEVELUP { NewLevel = (byte) level });

            return Done($"Set the level to {level}.");
        }

        if (!target.Wizard.SetLevel((byte) level)) {
            return Refuse($"Could not set the level to {level}.");
        }

        target.Wizard.ResetExperiencePointsToLevel();

        return Done($"Set the level to {level}.");
    }

    internal static AdminActionResult AddExperience(CharacterTarget target, string xp) {
        if (!int.TryParse(xp, out var xpInt)) {
            return Refuse("Invalid XP amount.");
        }

        if (target.Online) {
            target.Tell(new CHARACTER_103_PROTOCOL.MSG_GAINXP { XP = xpInt });
        }
        else {
            target.Wizard.AddExperiencePoints(xpInt);
        }

        return Done($"Added {xpInt} XP.");
    }

    #endregion

    #region Gold

    internal static AdminActionResult SetMaxGold(CharacterTarget target, string gold) {
        if (!int.TryParse(gold, out var goldInt)) {
            return Refuse("Invalid maximum gold amount.");
        }

        target.Wizard.SetMaxGold(goldInt);
        target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = target.Wizard.GameStats.m_currentGold,
            MaxGold = goldInt
        });

        return Done($"Set max gold to {goldInt}.");
    }

    internal static AdminActionResult AddGold(CharacterTarget target, string gold) {
        if (!int.TryParse(gold, out var goldInt)) {
            return Refuse("Invalid gold amount.");
        }

        target.Wizard.AddGold(goldInt);
        target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = target.Wizard.GameStats.m_currentGold,
            MaxGold = target.Wizard.GameStats.m_baseGoldPouch
        });

        return Done($"Added {goldInt} gold.");
    }

    #endregion

    #region Health, mana and energy

    // These are rebuilt from the level at login, so they last until the player relogs.
    internal static AdminActionResult SetMaxHealth(CharacterTarget target, string health) {
        if (!int.TryParse(health, out var healthInt)) {
            return Refuse("Invalid maximum health amount.");
        }

        target.Wizard.GameStats.m_baseHitpoints = healthInt;
        target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
            CharacterID = target.Wizard.GameObjectID,
            NewHealth = target.Wizard.GameStats.m_currentHitpoints,
            NewHealthMax = healthInt,
            DisplayDiff = 1,
        });

        return Done($"Set max health to {healthInt}.");
    }

    internal static AdminActionResult SetMaxMana(CharacterTarget target, string mana) {
        if (!int.TryParse(mana, out var manaInt)) {
            return Refuse("Invalid maximum mana amount.");
        }

        target.Wizard.GameStats.m_baseMana = manaInt;
        target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
            Mana = target.Wizard.GameStats.m_currentMana,
            MaxMana = manaInt,
            DisplayDiff = 1,
        });

        return Done($"Set max mana to {manaInt}.");
    }

    internal static AdminActionResult SetMaxEnergy(CharacterTarget target, string energy) {
        if (!int.TryParse(energy, out var energyInt)) {
            return Refuse("Invalid maximum energy amount.");
        }

        var wizard = target.Wizard;
        wizard.GameStats.m_energyMax = energyInt;
        wizard.PetOwnerBehavior.SetEnergy(energyInt);

        target.Tell(new PET_9_PROTOCOL.MSG_PETENERGYTICK {
            GlobalID = wizard.GameObjectID,
            Energy = wizard.PetOwnerBehavior.Energy,
            MaxEnergy = energyInt,
            TickTime = (int) wizard.PetOwnerBehavior.LastEnergyTickEpoch
        });
        target.Tell(new PET_9_PROTOCOL.MSG_PETENERGYMAX { MaxEnergy = energyInt });

        return Done($"Set max energy to {energyInt}.");
    }

    internal static AdminActionResult SetHealth(CharacterTarget target, string health) {
        if (!int.TryParse(health, out var healthInt)) {
            return Refuse("Invalid current health amount.");
        }

        var wizard = target.Wizard;
        wizard.UpdateHealth(Math.Min(healthInt, wizard.GameStats.m_baseHitpoints));
        SendHealth(target, healthInt);

        return Done($"Set current health to {healthInt}.");
    }

    internal static AdminActionResult SetMana(CharacterTarget target, string mana) {
        if (!int.TryParse(mana, out var manaInt)) {
            return Refuse("Invalid current mana amount.");
        }

        var wizard = target.Wizard;
        wizard.UpdateMana(Math.Min(manaInt, wizard.GameStats.m_baseMana));
        SendMana(target, manaInt);

        return Done($"Set current mana to {manaInt}.");
    }

    internal static AdminActionResult SetEnergy(CharacterTarget target, string energy) {
        if (!int.TryParse(energy, out var energyInt)) {
            return Refuse("Invalid current energy amount.");
        }

        var wizard = target.Wizard;
        wizard.UpdateEnergy(Math.Min(energyInt, wizard.GameStats.m_energyMax));
        SendEnergy(target, energyInt);

        return Done($"Set current energy to {energyInt}.");
    }

    internal static AdminActionResult RefillHealth(CharacterTarget target) {
        var maxHealth = target.Wizard.GameStats.m_baseHitpoints;
        target.Wizard.UpdateHealth(maxHealth);
        SendHealth(target, maxHealth);

        return Done("Refilled health.");
    }

    internal static AdminActionResult RefillMana(CharacterTarget target) {
        var maxMana = target.Wizard.GameStats.m_baseMana;
        target.Wizard.UpdateMana(maxMana);
        SendMana(target, maxMana);

        return Done("Refilled mana.");
    }

    internal static AdminActionResult RefillEnergy(CharacterTarget target) {
        var maxEnergy = GetLevelStats(target).m_petEnergy;
        target.Wizard.UpdateEnergy(maxEnergy);
        SendEnergy(target, maxEnergy);

        return Done("Refilled energy.");
    }

    // The client has the max health, mana and energy increases of the character's gear applied, so the maximums
    // sent are the level's own; sending the totals would double the increases client side.
    private static void SendHealth(CharacterTarget target, int health)
        => target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
            CharacterID = target.Wizard.GameObjectID,
            NewHealth = health,
            NewHealthMax = GetLevelStats(target).m_hitpoints,
            DisplayDiff = 1,
        });

    private static void SendMana(CharacterTarget target, int mana)
        => target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
            Mana = mana,
            MaxMana = GetLevelStats(target).m_mana,
            DisplayDiff = 1,
        });

    private static void SendEnergy(CharacterTarget target, int energy) {
        var maxEnergy = GetLevelStats(target).m_petEnergy;
        target.Tell(new PET_9_PROTOCOL.MSG_PETENERGYTICK {
            GlobalID = target.Wizard.GameObjectID,
            Energy = energy,
            MaxEnergy = maxEnergy,
            TickTime = (int) target.Wizard.PetOwnerBehavior.LastEnergyTickEpoch
        });
    }

    private static MagicLevelInfo GetLevelStats(CharacterTarget target) {
        var school = target.Wizard.MagicSchoolBehavior;

        return MagicLevelsConfig.GetPlayerLevelInfo(school.MagicSchool, school.Level);
    }

    #endregion

    #region Cantrips, training points and potions

    internal static AdminActionResult CantripLevelUp(CharacterTarget target)
        => SetCantripLevel(target, target.Wizard.GameStats.m_cantripLevel + 1);

    internal static AdminActionResult SetCantripLevel(CharacterTarget target, string level)
        => byte.TryParse(level, out var levelByte) ? SetCantripLevel(target, levelByte) : Refuse("Invalid level.");

    private static AdminActionResult SetCantripLevel(CharacterTarget target, int level) {
        var maxLevel = CantripFactory.GetMaxCantripLevel();
        if (level > maxLevel) {
            return Refuse($"You cannot set level higher than the max level ({maxLevel}).");
        }

        target.Wizard.UpdateCantripLevel((byte) level);
        target.Tell(new CANTRIPSMESSAGES_57_PROTOCOL.MSG_UPDATECANTRIPXP { XP = 0, Level = (byte) level });

        return Done($"Set the cantrip level to {level}.");
    }

    internal static AdminActionResult AddTrainingPoints(CharacterTarget target, string trainingPoints) {
        if (!int.TryParse(trainingPoints, out var trainingPointsInt)) {
            return Refuse("Invalid training points amount.");
        }

        var newTrainingPoints = target.Wizard.MagicSchoolBehavior.TrainingPoints + trainingPointsInt;
        target.Wizard.UpdateTrainingPoints(newTrainingPoints);
        target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING { TrainingPoints = newTrainingPoints });

        return Done($"Added {trainingPointsInt} training points.");
    }

    internal static AdminActionResult SetTrainingPoints(CharacterTarget target, string trainingPoints) {
        if (!int.TryParse(trainingPoints, out var trainingPointsInt)) {
            return Refuse("Invalid training points amount.");
        }

        target.Wizard.UpdateTrainingPoints(trainingPointsInt);
        target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING { TrainingPoints = trainingPointsInt });

        return Done($"Set training points to {trainingPointsInt}.");
    }

    internal static AdminActionResult SetPotionMax(CharacterTarget target, string potionMax) {
        if (!int.TryParse(potionMax, out var potionMaxInt)) {
            return Refuse("Invalid potion amount.");
        }

        target.Wizard.UpdatePotions(potionMaxInt, potionMaxInt);
        target.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEPOTIONS { PotionMax = potionMaxInt, PotionCharge = potionMaxInt });

        return Done($"Set and filled potions to {potionMaxInt}.");
    }

    #endregion

    #region Name, badge title and speed

    internal static AdminActionResult SetNameOverride(CharacterTarget target, string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return Refuse("Enter the new name.");
        }

        target.Wizard.SetNameOverride(name);

        return Done($"Set name to {name}. Relog to see changes.");
    }

    internal static AdminActionResult SetBadgeTitle(CharacterTarget target, string badge) {
        badge = badge?.Trim() ?? "";

        // A live character's badge service sets the title and tells the zone, so nobody has to relog.
        if (target.Online) {
            target.Tell(new CHARACTER_103_PROTOCOL.MSG_BADGECOMMAND {
                Action = "title",
                Argument = badge,
                ReplyTo = target.Replies?.ReplyTo,
                RequestId = target.Replies?.RequestId ?? 0
            });

            return AdminActionResult.Sent();
        }

        target.Wizard.SetBadgeOverride(badge);

        return Done($"Set the badge title to {badge}.");
    }

    internal static AdminActionResult SetSpeed(CharacterTarget target, string speedBonus) {
        if (!int.TryParse(speedBonus, out var speedBonusInt)) {
            return Refuse("Invalid speed multiplier.");
        }

        var effect = new SpeedEffect {
            m_speedMultiplier = speedBonusInt, // Speed is not a multiplier, but a percentage bonus! (+50%, etc.)
            m_effectNameID = SpeedEffectName,
            m_itemSlotID = 0 //  Should be 0 when effects are not bound to an equipped gear slot!
        };

        var serializer = new CoreObjectSerializer(behaviors: SerializerFlags.None);
        var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!serializer.Serialize(effect, flags, out var serializedEffect)) {
            return Refuse("Failed to serialize speed effect.");
        }

        target.Tell(new GAME_5_PROTOCOL.MSG_ADDEFFECT {
            GameObjectID = target.Wizard.GameObjectID,
            EffectData = serializedEffect
        });

        return Done(speedBonusInt > 0 ? $"Increased speed by {speedBonusInt}%." : $"Decreased speed by {speedBonusInt}%.");
    }

    #endregion

}
