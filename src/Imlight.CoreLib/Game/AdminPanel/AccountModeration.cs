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
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// The moderation actions shared by the admin panel and the chat commands: infractions, mutes, bans, kicks and
/// account deletion.
/// </summary>
/// <remarks>
/// Each action checks the acting account itself, as <see cref="AccountAdministration"/> does, and every change is
/// written to the admin audit log. An online player's session keeps its own copy of the account, so a change it
/// checks while the player plays (a mute) is also sent to that session; a ban or kick closes the session, in
/// whichever zone or realm it is.
/// </remarks>
internal static class AccountModeration {

    private const string KickedMessage = "You have been kicked from the server.";

    internal static AdminActionResult Info(Account actor, string username) {
        if (!TryFind(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        var history = account.InfractionHistory;
        var lines = new List<string> {
            $"Auth level: {account.AuthLevel}",
            $"Created: {account.CreationTime}",
            $"Last login: {account.LastLoginTime}",
            $"Last login machine ID: {account.LastLoginMachineId}",
            $"Last login IP: {account.LastLoginIp}",
            $"Locked: {account.IsLocked}",
        };
        if (history.IsCurrentlyBanned) {
            lines.Add($"Ban ends at: {history.BanEndsAt}");
        }

        if (history.IsCurrentlyMuted) {
            lines.Add($"Mute ends at: {history.MuteEndsAt}");
        }

        lines.Add(history.Infractions.Count > 0 ? $"Infractions: {history.Infractions.Count}" : "No infractions.");
        lines.Add($"Character IDs: {string.Join(", ", account.CharacterIds)}");

        return AdminActionResult.Done(account.Username, lines);
    }

    internal static AdminActionResult Infractions(Account actor, string username) {
        if (!TryFind(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        var infractions = account.InfractionHistory.Infractions;
        if (infractions.Count == 0) {
            return AdminActionResult.Done("No infractions.");
        }

        var lines = infractions
            .Select((infraction, i) => $"[{i + 1}] {infraction.InfractionType} {infraction.InfractionTime} ({infraction.ResponsibleModerator}): {infraction.Reason}")
            .ToList();

        return AdminActionResult.Done($"Infractions of {account.Username}:", lines);
    }

    internal static AdminActionResult Warn(Account actor, Wizard character, string username, string reason) {
        if (!TryFindOutranked(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        if (account.AddInfraction(InfractionType.Warn, reason, actor.Username) is null) {
            return AdminActionResult.Refuse("Infraction add failed.");
        }

        AccountAdministration.Audit(actor, character, AdminAuditAction.InfractionAdded, $"{account.Username}: warning: {reason}");

        return AdminActionResult.Done($"Warned {account.Username}.");
    }

    /// <param name="number">The infraction's number in the list, from 1.</param>
    internal static AdminActionResult RemoveInfraction(Account actor, Wizard character, string username, string number) {
        if (!int.TryParse(number, out var index)) {
            return AdminActionResult.Refuse("Invalid infraction index.");
        }

        if (!TryFindOutranked(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        var infractions = account.InfractionHistory.Infractions;
        var removed = index >= 1 && index <= infractions.Count ? infractions[index - 1] : null;
        if (removed is null || !account.RemoveInfraction(index - 1)) {
            return AdminActionResult.Refuse("Infraction remove failed.");
        }

        AccountAdministration.Audit(actor, character, AdminAuditAction.InfractionRemoved,
            $"{account.Username}: {removed.InfractionType}: {removed.Reason}");

        return AdminActionResult.Done("Infraction removed successfully.");
    }

    internal static AdminActionResult Mute(Account actor, Wizard character, IActorRefFactory actors, string username,
                                           string duration, string reason) {
        if (!TryFindOutranked(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        if (account.InfractionHistory.IsCurrentlyMuted) {
            return AdminActionResult.Refuse("This account is already muted.");
        }

        if (!CommandUtilities.TryParseDuration(duration, out var span)) {
            return AdminActionResult.Refuse("Invalid duration.");
        }

        var end = DateTime.UtcNow.Add(span);
        account.AddInfraction(InfractionType.Mute, reason, actor.Username, end);
        TellSessions(actors, account, new ACCOUNT_104_PROTOCOL.MSG_ACCOUNTCHANGED());
        AccountAdministration.Audit(actor, character, AdminAuditAction.MuteAdded, $"{account.Username} until {end:u}: {reason}");

        return AdminActionResult.Done($"Muted {account.Username} until {end}.");
    }

    internal static AdminActionResult Unmute(Account actor, Wizard character, IActorRefFactory actors, string username) {
        if (!TryFindOutranked(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        if (!account.InfractionHistory.IsCurrentlyMuted) {
            return AdminActionResult.Refuse("This account is not muted.");
        }

        account.WaiveCurrentMute(actor.Username);
        TellSessions(actors, account, new ACCOUNT_104_PROTOCOL.MSG_ACCOUNTCHANGED());
        AccountAdministration.Audit(actor, character, AdminAuditAction.MuteRemoved, account.Username);

        return AdminActionResult.Done($"Unmuted {account.Username}.");
    }

    internal static AdminActionResult Kick(Account actor, Wizard character, IActorRefFactory actors, string username,
                                           string reason) {
        if (!TryFindOutranked(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        if (!CloseSessions(actors, account)) {
            return AdminActionResult.Refuse($"{account.Username} is not online.");
        }

        AccountAdministration.Audit(actor, character, AdminAuditAction.PlayerKicked, $"{account.Username}: {reason}");

        return AdminActionResult.Done($"Kicked {account.Username}.");
    }

    internal static AdminActionResult Ban(Account actor, Wizard character, IActorRefFactory actors, string username,
                                          string duration, string reason) {
        if (!TryFindOutranked(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        if (account.InfractionHistory.IsCurrentlyBanned) {
            return AdminActionResult.Refuse("Account is already banned");
        }

        if (!CommandUtilities.TryParseDuration(duration, out var span)) {
            return AdminActionResult.Refuse("Invalid time format");
        }

        var end = DateTime.UtcNow + span;
        account.AddInfraction(InfractionType.Ban, reason, actor.Username, end);
        CloseSessions(actors, account);
        AccountAdministration.Audit(actor, character, AdminAuditAction.BanAdded, $"{account.Username} until {end:u}: {reason}");

        return AdminActionResult.Done($"Account {account.Username} has been banned until {end}");
    }

    internal static AdminActionResult Unban(Account actor, Wizard character, string username) {
        if (!TryFindOutranked(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        if (!account.InfractionHistory.IsCurrentlyBanned) {
            return AdminActionResult.Refuse("Account is not banned.");
        }

        if (!account.WaiveCurrentBan(actor.Username)) {
            return AdminActionResult.Refuse("Failed to waive ban.");
        }

        AccountAdministration.Audit(actor, character, AdminAuditAction.BanRemoved, account.Username);

        return AdminActionResult.Done("Account unbanned.");
    }

    internal static AdminActionResult BanInfo(Account actor, string username) {
        if (!TryFind(actor, username, out var account, out var refusal)) {
            return refusal;
        }

        var history = account.InfractionHistory;
        var lines = new List<string> {
            $"Banned: {history.IsCurrentlyBanned}" + (history.IsCurrentlyBanned ? $", until {history.BanEndsAt}" : ""),
            $"Muted: {history.IsCurrentlyMuted}" + (history.IsCurrentlyMuted ? $", until {history.MuteEndsAt}" : ""),
            history.Infractions.Count > 0 ? $"Last infraction: {history.LastInfractionTime}" : "No infractions.",
        };

        return AdminActionResult.Done($"Account: {account.Username}", lines);
    }

    internal static AdminActionResult BanIp(Account actor, Wizard character, string ip, string duration, string reason) {
        if (!AccountAdministration.CanAct(actor, AuthLevel.HallMonitor, out var refusal)) {
            return refusal;
        }

        ip = ip?.Trim() ?? "";
        if (InfractionCollection.IsIpBanned(ip)) {
            return AdminActionResult.Refuse("IP is already banned");
        }

        if (!CommandUtilities.TryParseDuration(duration, out var span)) {
            return AdminActionResult.Refuse("Invalid time format");
        }

        var end = DateTime.UtcNow + span;
        InfractionCollection.AddIpBan(ip, end);
        AccountAdministration.Audit(actor, character, AdminAuditAction.BanAdded, $"IP {ip} until {end:u}: {reason}");

        return AdminActionResult.Done($"IP {ip} has been banned until {end}");
    }

    internal static AdminActionResult UnbanIp(Account actor, Wizard character, string ip) {
        if (!AccountAdministration.CanAct(actor, AuthLevel.HallMonitor, out var refusal)) {
            return refusal;
        }

        ip = ip?.Trim() ?? "";
        if (!InfractionCollection.IsIpBanned(ip)) {
            return AdminActionResult.Refuse("IP is not banned");
        }

        if (!InfractionCollection.RemoveIpBan(ip)) {
            return AdminActionResult.Refuse("Failed to unban IP.");
        }

        AccountAdministration.Audit(actor, character, AdminAuditAction.BanRemoved, $"IP {ip}");

        return AdminActionResult.Done("IP unbanned.");
    }

    internal static AdminActionResult BanMachine(Account actor, Wizard character, string machineId, string duration,
                                                 string reason) {
        if (!AccountAdministration.CanAct(actor, AuthLevel.HallMonitor, out var refusal)) {
            return refusal;
        }

        if (!ulong.TryParse(machineId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)) {
            return AdminActionResult.Refuse("Invalid machine ID");
        }

        if (InfractionCollection.IsMachineBanned(id)) {
            return AdminActionResult.Refuse("Machine is already banned");
        }

        if (!CommandUtilities.TryParseDuration(duration, out var span)) {
            return AdminActionResult.Refuse("Invalid time format");
        }

        var end = DateTime.UtcNow + span;
        InfractionCollection.AddMachineBan(id, end);
        AccountAdministration.Audit(actor, character, AdminAuditAction.BanAdded, $"machine {id} until {end:u}: {reason}");

        return AdminActionResult.Done($"Machine {id} has been banned until {end}");
    }

    internal static AdminActionResult UnbanMachine(Account actor, Wizard character, string machineId) {
        if (!AccountAdministration.CanAct(actor, AuthLevel.HallMonitor, out var refusal)) {
            return refusal;
        }

        if (!ulong.TryParse(machineId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)) {
            return AdminActionResult.Refuse("Invalid machine ID");
        }

        if (!InfractionCollection.IsMachineBanned(id)) {
            return AdminActionResult.Refuse("Machine is not banned");
        }

        if (!InfractionCollection.RemoveMachineBan(id)) {
            return AdminActionResult.Refuse("Failed to unban machine.");
        }

        AccountAdministration.Audit(actor, character, AdminAuditAction.BanRemoved, $"machine {id}");

        return AdminActionResult.Done("Machine unbanned.");
    }

    internal static AdminActionResult DeleteAccount(Account actor, Wizard character, IActorRefFactory actors,
                                                    string username) {
        if (!AccountAdministration.CanActOn(actor, AuthLevel.Administrator, username, out var account, out var refusal)) {
            return refusal;
        }

        if (account.AccountId == actor.AccountId) {
            return AdminActionResult.Refuse("You cannot delete your own account.");
        }

        CloseSessions(actors, account);
        if (!AccountCollection.DeleteAccount(account.Username)) {
            return AdminActionResult.Refuse($"Account {account.Username} could not be deleted.");
        }

        AccountAdministration.Audit(actor, character, AdminAuditAction.AccountDeleted, account.Username);

        return AdminActionResult.Done($"Account {account.Username} deleted.");
    }

    // Viewing needs only the level that can view accounts.
    private static bool TryFind(Account actor, string username, out Account account, out AdminActionResult refusal) {
        account = null;
        if (!AccountAdministration.CanAct(actor, AuthLevel.HallMonitor, out refusal)) {
            return false;
        }

        account = AccountCollection.GetAccountWithoutCharacters(username?.Trim() ?? "", waitForIndex: true);
        refusal = account is null ? AdminActionResult.Refuse("Account not found.") : null;

        return account is not null;
    }

    private static bool TryFindOutranked(Account actor, string username, out Account account,
                                         out AdminActionResult refusal)
        => TryFind(actor, username, out account, out refusal) && AccountAdministration.CanActOn(actor, account, out refusal);

    private static List<OnlinePlayer> GetSessions(Account account)
        => [.. OnlinePlayerCollection.GetOnlinePlayers()
            .Where(player => player.AccountId == account.AccountId && !string.IsNullOrEmpty(player.ActorPath))];

    private static void TellSessions(IActorRefFactory actors, Account account, object message) {
        foreach (var player in GetSessions(account)) {
            actors?.ActorSelection(player.ActorPath).Tell(message);
        }
    }

    // Returns whether the account had a session to close.
    private static bool CloseSessions(IActorRefFactory actors, Account account) {
        var sessions = GetSessions(account);
        foreach (var player in sessions) {
            var session = actors?.ActorSelection(player.ActorPath);
            session?.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE { Message = KickedMessage, Modal = 1 });
            session?.Tell("Close");
        }

        return sessions.Count > 0;
    }

}
