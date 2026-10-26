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
using System.Globalization;
using System.Linq;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Answers the admin panel's Players page: look an account up to see its Crowns, then change them.
/// </summary>
/// <remarks>
/// Requests (fields after the action):
///   lookup NAME
///   crowns NAME CHANGE      CHANGE is a signed whole number added to the balance
/// Replies:
///   status KIND MESSAGE                    KIND is ok, error or info; a successful lookup sends none
///   player NAME CROWNS SUMMARY DETAILS     sent after a lookup and after every applied change
/// Changes are always relative, so a purchase made while the page is open is never overwritten.
/// </remarks>
internal static class PlayersPage {

    internal const string Prefix = "players.";

    /// <param name="adjustOnline">Applies a Crowns change through the account's online session, or returns null
    /// when the account is not online.</param>
    internal static void Handle(Account actor, Wizard character, string request, string[] fields,
                                Action<string[]> reply, Func<ulong, int, CrownChange> adjustOnline) {
        string Field(int index) => index < fields.Length ? fields[index] : "";

        switch (request) {
            case "lookup":
                Lookup(actor, Field(0), reply);
                break;
            case "crowns":
                ChangeCrowns(actor, character, Field(0), Field(1), reply, adjustOnline);
                break;
            default:
                reply(["status", "error", $"Unknown request '{request}'."]);
                break;
        }
    }

    private static void Lookup(Account actor, string username, Action<string[]> reply) {
        if (string.IsNullOrWhiteSpace(username)) {
            reply(["status", "error", "Type a username first."]);

            return;
        }

        var result = AccountAdministration.Lookup(actor, username, out var account);
        if (!result.Succeeded) {
            reply(["status", "error", result.Message]);

            return;
        }

        // The filled-in fields show that it loaded.
        SendPlayer(account, reply);
    }

    private static void ChangeCrowns(Account actor, Wizard character, string username, string change,
                                     Action<string[]> reply, Func<ulong, int, CrownChange> adjustOnline) {
        if (!int.TryParse(change.Replace(",", "").Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out var amount)) {
            reply(["status", "error", "Enter a whole number of Crowns, like 500 or -500."]);

            return;
        }

        var result = AccountAdministration.AdjustCrowns(actor, character, username, amount, adjustOnline, out var balance);
        if (result.Succeeded) {
            SendPlayer(AccountCollection.GetAccount(username.Trim(), waitForIndex: true), reply, balance.After);
        }

        reply(["status", result.Succeeded ? "ok" : "error", result.Message]);
    }

    // An online player's balance is saved as it changes, so the stored one is current; after a change the
    // balance the change produced is shown, in case the stored one is read before the write lands.
    private static void SendPlayer(Account account, Action<string[]> reply, int? crowns = null) {
        var online = OnlinePlayerCollection.GetOnlinePlayers().FirstOrDefault(player => player.AccountId == account.AccountId);
        var presence = online is null ? "offline" : $"online in {online.CurrentZone}";
        var summary = $"{account.Username}: {account.AuthLevel}, {presence}";
        var details = $"{account.CharacterIds.Count} character{(account.CharacterIds.Count == 1 ? "" : "s")}";

        reply(["player", account.Username, AccountAdministration.FormatCrowns(crowns ?? account.Crowns), summary, details]);
    }

}
