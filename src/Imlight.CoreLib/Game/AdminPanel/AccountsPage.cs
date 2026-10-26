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
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Answers the admin panel's Accounts page: look an account up, then save changes to it, or create one.
/// </summary>
/// <remarks>
/// Requests (fields after the action; an empty field means "unchanged"):
///   lookup NAME
///   create NAME PASSWORD LEVEL LOCKED
///   save   NAME LEVEL LOCKED PASSWORD
/// Replies:
///   status KIND MESSAGE                         KIND is ok, error or info; a successful lookup sends none
///   account NAME LEVEL LOCKED SUMMARY DETAILS   sent after every request that finds or changes an account
/// </remarks>
internal static class AccountsPage {

    internal const string Prefix = "accounts.";

    internal static void Handle(Account actor, Wizard character, string request, string[] fields,
                                Action<string[]> reply) {
        string Field(int index) => index < fields.Length ? fields[index] : "";

        switch (request) {
            case "lookup":
                Lookup(actor, Field(0), reply);
                break;
            case "create":
                Create(actor, character, Field(0), Field(1), Field(2), Field(3), reply);
                break;
            case "save":
                Save(actor, character, Field(0), Field(1), Field(2), Field(3), reply);
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
            var hint = account is null && actor.AdminModeEnabled ? " To make it, enter a password and press Create." : "";
            reply(["status", "error", result.Message + hint]);

            return;
        }

        // The filled-in fields show that it loaded.
        SendAccount(account, reply);
    }

    private static void Create(Account actor, Wizard character, string username, string password, string level,
                               string locked, Action<string[]> reply) {
        var created = AccountAdministration.CreateAccount(actor, character, username, password);
        if (!created.Succeeded) {
            reply(["status", "error", created.Message]);

            return;
        }

        var done = new List<string> { created.Message };
        string failure = null;
        username = username.Trim();
        if (!string.IsNullOrEmpty(level) && !level.Equals(nameof(AuthLevel.None), StringComparison.OrdinalIgnoreCase)) {
            Apply(AccountAdministration.SetAuthLevel(actor, character, username, level), done, ref failure);
        }

        if (locked == "1" && failure is null) {
            Apply(AccountAdministration.SetLocked(actor, character, username, true), done, ref failure);
        }

        Finish(username, done, failure, reply);
    }

    private static void Save(Account actor, Wizard character, string username, string level, string locked,
                             string password, Action<string[]> reply) {
        if (string.IsNullOrEmpty(level) && string.IsNullOrEmpty(locked) && string.IsNullOrEmpty(password)) {
            reply(["status", "info", "Nothing to save: nothing was changed."]);

            return;
        }

        var done = new List<string>();
        string failure = null;
        if (!string.IsNullOrEmpty(level)) {
            Apply(AccountAdministration.SetAuthLevel(actor, character, username, level), done, ref failure);
        }

        if (!string.IsNullOrEmpty(locked) && failure is null) {
            Apply(AccountAdministration.SetLocked(actor, character, username, locked == "1"), done, ref failure);
        }

        if (!string.IsNullOrEmpty(password) && failure is null) {
            Apply(AccountAdministration.ChangePassword(actor, character, username, password), done, ref failure);
        }

        Finish(username, done, failure, reply);
    }

    private static void Apply(AdminActionResult result, List<string> done, ref string failure) {
        if (result.Succeeded) {
            done.Add(result.Message);
        }
        else {
            failure = result.Message;
        }
    }

    // Sends the account as it is now stored, then what happened. A partial save says what was and wasn't done.
    private static void Finish(string username, List<string> done, string failure, Action<string[]> reply) {
        var account = AccountCollection.GetAccount(username?.Trim() ?? "", waitForIndex: true);
        if (account is not null) {
            SendAccount(account, reply);
        }

        if (failure is null) {
            reply(["status", "ok", string.Join(" ", done)]);
        }
        else if (done.Count == 0) {
            reply(["status", "error", failure]);
        }
        else {
            reply(["status", "error", $"{string.Join(" ", done)} Stopped: {failure}"]);
        }
    }

    private static void SendAccount(Account account, Action<string[]> reply) {
        var history = account.InfractionHistory;
        // Login times are stored in UTC; staff read them in the server's time zone.
        var lastLogin = account.LastLoginTime == default
            ? "never logged in"
            : $"last login {DateTime.SpecifyKind(account.LastLoginTime, DateTimeKind.Utc).ToLocalTime():M/d/yyyy h:mm tt}";
        var summary = $"Created {account.CreationTime:M/d/yyyy}, {lastLogin}";

        var details = new List<string> {
            Count(account.CharacterIds.Count, "character"),
            Count(history.Infractions.Count, "infraction")
        };
        if (history.IsCurrentlyBanned) {
            details.Add($"banned until {history.BanEndsAt:M/d/yyyy}");
        }

        if (history.IsCurrentlyMuted) {
            details.Add($"muted until {history.MuteEndsAt:M/d/yyyy}");
        }

        if (!string.IsNullOrEmpty(account.LastLoginIp)) {
            details.Add($"last IP {StripPort(account.LastLoginIp)}");
        }

        reply(["account", account.Username, account.AuthLevel.ToString(), account.IsLocked ? "1" : "0", summary,
               string.Join(", ", details)]);
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    // Session addresses are stored as address:port; only IPv4 has exactly one colon.
    private static string StripPort(string address) {
        var colon = address.IndexOf(':');

        return colon > 0 && colon == address.LastIndexOf(':') ? address[..colon] : address;
    }

}
