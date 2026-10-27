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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Answers the admin panel's username bar and character column, and runs the character tabs' actions.
/// </summary>
/// <remarks>
/// Requests (fields after the action):
///   lookup NAME
///   character.select CHARID
///   character.run CHARID ACTION ARGUMENT…
/// Replies:
///   account …, player …                         as the Accounts and Players pages send them, for HallMonitor and up
///   chars NAME COUNT                            then one char reply per character
///   char INDEX ID NAME SCHOOL GENDER LEVEL ONLINE ZONE    INDEX is "-" when one character is refreshed
///   charinfo ID LINE…                           the character's stats, shown in the action pages' output
///   status KIND MESSAGE                         KIND is ok, error or info
///   output MODE LINE…                           MODE is set (replace) or add
/// Anyone with a panel may look up their own account; another account needs HallMonitor, as viewing accounts does.
/// A character action needs the level of its chat command, and on another account's character also HallMonitor and
/// outranking that account. An online character is changed by its own session, so the action is sent there and
/// answered later; an offline one is loaded and changed here.
/// </remarks>
internal static class CharactersPage {

    internal const string Prefix = "character.";

    /// <summary>
    /// Sends an action to the session of an online character, at its actor path.
    /// </summary>
    internal delegate void SessionSender(string actorPath, ADMIN_112_PROTOCOL.MSG_ADMINCHARACTERACTION request);

    internal static void Lookup(Account actor, string username, Action<string[]> reply) {
        username = username?.Trim() ?? "";
        if (username.Length == 0) {
            reply(["status", "error", "Type a username first."]);

            return;
        }

        // Whether an account exists is only told to those who may view it.
        var account = AccountCollection.GetAccountForCharacterList(username);
        if (account?.AccountId != actor.AccountId && !AccountAdministration.CanAct(actor, AuthLevel.HallMonitor, out var refusal)) {
            reply(["status", "error", refusal.Message]);

            return;
        }

        if (account is null) {
            var hint = actor.AuthLevel >= AuthLevel.HallMonitor ? " To make it, enter a password on the Account tab and press Create." : "";
            reply(["status", "error", $"No account named {username}.{hint}"]);

            return;
        }

        if (actor.AuthLevel >= AuthLevel.HallMonitor) {
            AccountsPage.SendAccount(account, reply);
            PlayersPage.SendPlayer(account, reply);
        }

        // In the order they were made, as the character select screen lists them.
        var characters = account.CharacterIds
            .Select(id => account.Characters.FirstOrDefault(character => character.CharId == id))
            .Where(character => character is not null)
            .ToList();
        reply(["chars", account.Username, characters.Count.ToString(CultureInfo.InvariantCulture)]);
        for (var i = 0; i < characters.Count; i++) {
            reply(Head(characters[i], (i + 1).ToString(CultureInfo.InvariantCulture)));
        }
    }

    internal static void Handle(Account actor, Wizard actorCharacter, string request, string[] fields,
                                Action<string[]> reply, SessionSender sendToSession) {
        string Field(int index) => index < fields.Length ? fields[index] : "";

        if (!ulong.TryParse(Field(0), NumberStyles.None, CultureInfo.InvariantCulture, out var charId)) {
            reply(["status", "error", "Pick a character on the left first."]);

            return;
        }

        switch (request) {
            case "select":
                Select(actor, charId, reply);
                break;
            case "run":
                Run(actor, actorCharacter, charId, Field(1), fields.Length > 2 ? fields[2..] : [], reply, sendToSession);
                break;
            default:
                reply(["status", "error", $"Unknown request '{request}'."]);
                break;
        }
    }

    /// <summary>
    /// The lines of the character summary: level, experience, gold, health, mana, energy and where it is.
    /// </summary>
    internal static List<string> Describe(Wizard wizard, OnlinePlayer online) {
        var school = wizard.MagicSchoolBehavior;
        var stats = wizard.GameStats;
        var energy = wizard.PetOwnerBehavior?.Energy ?? 0;
        var place = online is null
            ? $"Offline, last in {wizard.ZoneDisplayName ?? wizard.Zone}"
            : $"Online in {online.CurrentZoneDisplayName ?? online.CurrentZone}";

        return [
            $"{wizard.PlayerNameBehavior.GetWizardName()}, level {school.Level} {school.MagicSchool}. {place}.",
            string.Create(CultureInfo.InvariantCulture,
                $"XP {school.ExperiencePoints:N0}, training points {school.TrainingPoints}, cantrip level {stats.m_cantripLevel}."),
            string.Create(CultureInfo.InvariantCulture,
                $"Gold {stats.m_currentGold:N0} of {stats.m_baseGoldPouch:N0}, potions {stats.m_potionCharge:0.#} of {stats.m_potionMax:0.#}."),
            $"Health {stats.m_currentHitpoints} of {stats.m_baseHitpoints}, mana {stats.m_currentMana} of {stats.m_baseMana}, energy {energy}.",
        ];
    }

    private static string[] Head(Wizard wizard, string index) {
        var online = OnlinePlayerCollection.GetOnlinePlayer(wizard.CharId);

        return [
            "char", index, wizard.CharId.ToString(CultureInfo.InvariantCulture), wizard.PlayerNameBehavior.GetWizardName(),
            wizard.MagicSchoolBehavior.MagicSchool.ToString(), wizard.WizardAvatar?.m_eGender.ToString() ?? "",
            wizard.MagicSchoolBehavior.Level.ToString(CultureInfo.InvariantCulture), online is null ? "0" : "1",
            online is null ? wizard.ZoneDisplayName ?? "" : online.CurrentZoneDisplayName ?? online.CurrentZone ?? ""
        ];
    }

    private static void Select(Account actor, ulong charId, Action<string[]> reply) {
        if (!TryFindCharacter(actor, charId, out var wizard, out _, out var refusal)) {
            reply(["status", "error", refusal.Message]);

            return;
        }

        SendCharacter(wizard, reply);
    }

    private static void SendCharacter(Wizard wizard, Action<string[]> reply) {
        reply(Head(wizard, "-"));
        reply(["charinfo", wizard.CharId.ToString(CultureInfo.InvariantCulture),
               .. Describe(wizard, OnlinePlayerCollection.GetOnlinePlayer(wizard.CharId))]);
    }

    private static void Run(Account actor, Wizard actorCharacter, ulong charId, string actionId, string[] fields,
                            Action<string[]> reply, SessionSender sendToSession) {
        var action = PanelActions.Find(actionId);
        if (action?.OnCharacter is null) {
            reply(["status", "error", $"Unknown action '{actionId}'."]);

            return;
        }

        if (!AccountAdministration.CanAct(actor, action.Level, out var refusal)
            || !PanelActions.TryReadArguments(action, fields, out var arguments, out refusal)
            || !TryFindCharacter(actor, charId, out var stored, out var account, out refusal)
            || (account.AccountId != actor.AccountId && !AccountAdministration.CanActOn(actor, account, out refusal))) {
            reply(["status", "error", refusal.Message]);

            return;
        }

        var online = OnlinePlayerCollection.GetOnlinePlayer(charId);
        if (online is not null && !string.IsNullOrEmpty(online.ActorPath)) {
            sendToSession(online.ActorPath, new ADMIN_112_PROTOCOL.MSG_ADMINCHARACTERACTION {
                CharId = charId,
                Action = action.Id,
                Arguments = arguments,
                Actor = actor,
                ActorCharacter = actorCharacter
            });

            return;
        }

        if (action.Reach == PanelReach.CharacterOnline) {
            reply(["status", "error", $"{stored.PlayerNameBehavior.GetWizardName()} is offline, and {action.Label} needs the character online."]);

            return;
        }

        // Loading runs the character's load fixes, as a login does; the player may log in meanwhile, so check again.
        var wizard = WizardCollection.GetCharacter(charId);
        if (wizard is null) {
            reply(["status", "error", "The character could not be loaded."]);

            return;
        }

        if (OnlinePlayerCollection.GetOnlinePlayer(charId) is not null) {
            reply(["status", "error", $"{wizard.PlayerNameBehavior.GetWizardName()} just logged in. Press Run again."]);

            return;
        }

        var result = PanelActions.RunOnCharacter(action, CharacterTarget.Offline(wizard), arguments, actor, actorCharacter);
        SendResult(result, reply);
        SendCharacter(wizard, reply);
    }

    /// <summary>
    /// Sends an action's outcome: its message as the status, and its listing, if any, as the output.
    /// </summary>
    internal static void SendResult(AdminActionResult result, Action<string[]> reply) {
        if (result.Deferred) {
            return;
        }

        reply(["output", "set", .. result.Lines ?? []]);
        reply(["status", result.Succeeded ? "ok" : "error", result.Message ?? ""]);
    }

    // Finds the stored character and its account, and checks the actor may view them.
    private static bool TryFindCharacter(Account actor, ulong charId, out Wizard wizard, out Account account,
                                         out AdminActionResult refusal) {
        account = null;
        wizard = WizardCollection.GetCharacterUnloaded(charId);
        if (wizard is null) {
            refusal = AdminActionResult.Refuse("That character no longer exists. Look the account up again.");

            return false;
        }

        if (wizard.AccountId != actor.AccountId && !AccountAdministration.CanAct(actor, AuthLevel.HallMonitor, out refusal)) {
            return false;
        }

        account = AccountCollection.GetAccountWithoutCharacters(wizard.AccountId);
        refusal = account is null ? AdminActionResult.Refuse("The character's account no longer exists.") : null;

        return account is not null;
    }

}
