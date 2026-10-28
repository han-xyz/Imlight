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
using Akka.Actor;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Answers the admin panel's Moderation tab: infractions, mutes, kicks and bans of the looked-up account, and bans
/// of an IP address or machine.
/// </summary>
/// <remarks>
/// Requests (fields after the action):
///   moderation.run NAME ACTION ARGUMENT…      NAME is the looked-up account; actions on an IP or machine ignore it
/// Replies:
///   status KIND MESSAGE, output MODE LINE…    as <see cref="CharactersPage"/> sends them
///   account …                                 the account as it now is, after a change to it
/// </remarks>
internal static class ModerationPage {

    internal const string Request = "moderation.run";

    internal static void Handle(Account actor, Wizard character, IActorRefFactory actors, string[] fields,
                                Action<string[]> reply) {
        var username = fields.Length > 0 ? fields[0].Trim() : "";
        var action = PanelActions.Find(fields.Length > 1 ? fields[1] : null);
        if (action?.OnAccount is null) {
            reply(["status", "error", "Unknown moderation action."]);

            return;
        }

        if (action.Reach == PanelReach.Account && username.Length == 0) {
            reply(["status", "error", "Look an account up first."]);

            return;
        }

        if (!PanelActions.TryReadArguments(action, fields.Length > 2 ? fields[2..] : [], out var arguments, out var refusal)) {
            reply(["status", "error", refusal.Message]);

            return;
        }

        var result = action.OnAccount(new AccountActionContext(actor, character, actors, username), arguments);
        CharactersPage.SendResult(result, reply);

        if (result.Succeeded && action.Changes && action.Reach == PanelReach.Account
            && AccountCollection.GetAccountWithoutCharacters(username, waitForIndex: true) is { } account) {
            AccountsPage.SendAccount(account, reply);
        }
    }

}
