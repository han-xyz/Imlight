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
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Auth;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// The outcome of a staff action. <paramref name="Lines"/> holds any listing it produced, one entry per line.
/// <paramref name="Deferred"/> means a session service carries the action out and answers by itself.
/// </summary>
internal sealed record AdminActionResult(bool Succeeded, string Message, IReadOnlyList<string> Lines = null,
                                         bool Deferred = false) {

    internal static AdminActionResult Done(string message, IReadOnlyList<string> lines = null)
        => new(true, message, lines);

    internal static AdminActionResult Refuse(string message) => new(false, message);

    internal static AdminActionResult Sent() => new(true, null, Deferred: true);

}

/// <summary>
/// The outcome of a Crowns change. <paramref name="Problem"/> is set when the outcome is unknown.
/// </summary>
internal sealed record CrownChange(bool Applied, int Before, int After, string Problem = null);

/// <summary>
/// The staff account actions shared by the admin panel, the chat commands and the server console.
/// </summary>
/// <remarks>
/// Each action checks the acting account itself, so callers need no checks of their own. Lacking the level for an
/// action is a plain refusal rather than an infraction: staff pressing a button they lack the level for are not
/// misbehaving. Acting on an account the actor does not outrank is recorded as suspicious, as the account commands
/// do. Every change is written to the admin audit log.
/// </remarks>
internal static class AccountAdministration {

    private static readonly TimeSpan s_onlineAdjustTimeout = TimeSpan.FromSeconds(5);

    internal static AdminActionResult CreateAccount(Account actor, Wizard character, string username, string password) {
        if (!CanAct(actor, AuthLevel.HallMonitor, out var refusal)) {
            return refusal;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) {
            return Refuse("Enter a username and a password.");
        }

        username = username.Trim();
        var created = AccountCollection.CreateAccount(new Account(username, "", DatabaseUtilities.CreateHashedPassword(password)));
        if (!created) {
            return Refuse($"Account {username} already exists.");
        }

        Audit(actor, character, AdminAuditAction.AccountCreated, username);

        return new(true, $"Account {username} created.");
    }

    internal static AdminActionResult ChangePassword(Account actor, Wizard character, string username, string password) {
        if (!CanActOn(actor, AuthLevel.Administrator, username, out var target, out var refusal)) {
            return refusal;
        }

        if (string.IsNullOrEmpty(password)) {
            return Refuse("Enter the new password.");
        }

        if (!AccountCollection.ChangePassword(target.Username, password)) {
            return Refuse("The password change failed.");
        }

        Audit(actor, character, AdminAuditAction.PasswordChanged, target.Username);

        return new(true, $"Password changed for {target.Username}.");
    }

    internal static AdminActionResult SetAuthLevel(Account actor, Wizard character, string username, string authLevel) {
        if (!CanActOn(actor, AuthLevel.Administrator, username, out var target, out var refusal)) {
            return refusal;
        }

        if (target.Username == actor.Username) {
            return Refuse("You cannot change the auth level of your own account.");
        }

        // Accept either the level's name or its number.
        if (!Enum.TryParse<AuthLevel>(authLevel?.Trim(), true, out var newAuthLevel) ||
            !Enum.IsDefined(newAuthLevel)) {
            return Refuse($"Invalid auth level. Use one of: {string.Join(", ", Enum.GetNames<AuthLevel>())}.");
        }

        // Nobody can grant a level above their own.
        if (newAuthLevel > actor.AuthLevel) {
            return Refuse("You cannot grant an auth level higher than your own.");
        }

        if (!AccountCollection.UpdateAuthLevel(target.Username, newAuthLevel)) {
            return Refuse("The auth level change failed.");
        }

        Audit(actor, character, AdminAuditAction.AuthLevelChanged, $"{target.Username}: {target.AuthLevel} -> {newAuthLevel}");

        return new(true, $"{target.Username} is now {newAuthLevel}.");
    }

    internal static AdminActionResult SetLocked(Account actor, Wizard character, string username, bool locked) {
        if (!CanActOn(actor, AuthLevel.HallMonitor, username, out var target, out var refusal)) {
            return refusal;
        }

        var changed = locked ? AccountCollection.LockAccount(target.Username) : AccountCollection.UnlockAccount(target.Username);
        if (!changed) {
            return Refuse(locked ? "The account lock failed." : "The account unlock failed.");
        }

        Audit(actor, character, locked ? AdminAuditAction.AccountLocked : AdminAuditAction.AccountUnlocked, target.Username);

        return new(true, locked ? $"Account {target.Username} locked." : $"Account {target.Username} unlocked.");
    }

    /// <summary>
    /// Finds an account for viewing. Needs only the level that can view accounts, not the target's level.
    /// </summary>
    internal static AdminActionResult Lookup(Account actor, string username, out Account account) {
        account = null;
        if (!CanAct(actor, AuthLevel.HallMonitor, out var refusal)) {
            return refusal;
        }

        account = AccountCollection.GetAccountWithoutCharacters(username?.Trim() ?? "", waitForIndex: true);

        return account is null ? Refuse($"No account named {username?.Trim()}.") : new(true, $"Found {account.Username}.");
    }

    /// <summary>
    /// Adds a signed amount of Crowns to an account; a negative amount takes Crowns away.
    /// </summary>
    /// <param name="adjustOnline">Applies the change through the account's online session and returns the outcome,
    /// or returns null when the account is not online.</param>
    internal static AdminActionResult AdjustCrowns(Account actor, Wizard character, string username, int change,
                                                   Func<ulong, int, CrownChange> adjustOnline, out CrownChange balance) {
        balance = null;
        if (!CanActOn(actor, AuthLevel.Administrator, username, out var target, out var refusal)) {
            return refusal;
        }

        if (change == 0) {
            return Refuse("Enter an amount of Crowns other than 0.");
        }

        balance = adjustOnline(target.AccountId, change);
        if (balance is null) {
            var applied = AccountCollection.TryAdjustCrowns(target.AccountId, change, out var after);
            balance = new(applied, applied ? after - change : after, after);
        }

        if (balance.Problem is not null) {
            return Refuse(balance.Problem);
        }

        if (!balance.Applied) {
            return Refuse(change < 0 && balance.Before + (long) change < 0
                ? $"{target.Username} has only {FormatCrowns(balance.Before)} Crowns."
                : "That is more Crowns than an account can hold.");
        }

        var signedChange = (change > 0 ? "+" : "-") + FormatCrowns(Math.Abs((long) change));
        Audit(actor, character, AdminAuditAction.CrownsChanged,
            $"{target.Username}: {balance.Before} -> {balance.After} ({signedChange})");

        return new(true, $"{target.Username} now has {FormatCrowns(balance.After)} Crowns ({signedChange}).");
    }

    /// <summary>
    /// Applies a Crowns change through an online account's session. Returns null when the account is not online.
    /// </summary>
    /// <remarks>
    /// An online player's session caches their Crowns, so the change is made there to keep that copy current;
    /// that session's Crown Shop also tells their client.
    /// </remarks>
    internal static CrownChange AdjustOnlineCrowns(IActorRefFactory actors, ulong accountId, int change) {
        var player = OnlinePlayerCollection.GetOnlinePlayers()
            .FirstOrDefault(x => x.AccountId == accountId && !string.IsNullOrEmpty(x.ActorPath));
        if (player is null) {
            return null;
        }

        try {
            var reply = actors.ActorSelection(player.ActorPath)
                .Ask<ACCOUNT_104_PROTOCOL.MSG_CROWNSADJUSTED>(new ACCOUNT_104_PROTOCOL.MSG_ADJUSTCROWNS { Change = change },
                    s_onlineAdjustTimeout)
                .Result;

            return new(reply.Applied, reply.Before, reply.After);
        }
        catch (Exception ex) {
            Logger.Warning("Admin panel: the session of account {0} did not answer a Crowns change: {1}",
                Logger.Args(accountId, ex.InnerException?.Message ?? ex.Message));

            return new(false, 0, 0, "The player's session did not answer, so the change may not have applied. Look the account up again before retrying.");
        }
    }

    internal static string FormatCrowns(long crowns) => crowns.ToString("N0", CultureInfo.InvariantCulture);

    internal static bool CanAct(Account actor, AuthLevel required, out AdminActionResult refusal) {
        refusal = null;
        if (actor.AuthLevel < required) {
            refusal = Refuse($"This needs auth level {required}; yours is {actor.AuthLevel}.");
        }

        return refusal is null;
    }

    internal static bool CanActOn(Account actor, AuthLevel required, string username, out Account target,
                                  out AdminActionResult refusal) {
        target = null;
        if (!CanAct(actor, required, out refusal)) {
            return false;
        }

        target = AccountCollection.GetAccountWithoutCharacters(username?.Trim() ?? "", waitForIndex: true);
        if (target is null) {
            refusal = Refuse("Account not found.");

            return false;
        }

        return CanActOn(actor, target, out refusal);
    }

    /// <summary>
    /// Checks that the actor outranks an account already loaded; the level the action needs is checked separately.
    /// </summary>
    internal static bool CanActOn(Account actor, Account target, out AdminActionResult refusal) {
        refusal = null;
        if (!AuthorityRequester.RequestAuthorityOver(target, actor, $"{actor.Username} used an admin action on account {target.Username}.")) {
            refusal = Refuse($"You do not outrank {target.Username}.");
        }

        return refusal is null;
    }

    internal static AdminActionResult Refuse(string message) => AdminActionResult.Refuse(message);

    internal static void Audit(Account actor, Wizard character, AdminAuditAction action, string detail) {
        var ip = actor.SessionActor?.Ip;
        Logger.Warning("Admin audit: {0} by account {1} ({2}), character {3}, IP {4}. {5}",
            Logger.Args(action, actor.Username, actor.AccountId, character?.CharId, ip ?? "unknown", detail ?? ""));

        AdminAuditCollection.AddEntry(new AdminAuditEntry {
            TimeStamp = DateTime.UtcNow,
            Action = action,
            AccountId = actor.AccountId,
            Username = actor.Username,
            CharacterId = character?.CharId ?? 0,
            ZoneName = character?.Zone,
            Ip = ip,
            Detail = detail
        });
    }

}
