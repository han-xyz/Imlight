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

using Imlight.CoreLib.Auth;
using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Text;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandAccountProtocol : CommandProtocol {

    internal override string Group { get; set; } = "account";

    [Command("create")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void CreateAccountCommand(string username, [Sensitive] string password) {
        Reply(AccountAdministration.CreateAccount(Context.Account, Context.Character, username, password));
    }

    [Command("delete")]
    [AuthRequired(AuthLevel.Administrator)]
    private void DeleteAccountCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");

            return;
        }

        var authorityReason = $"{Context.Account.Username} wants to delete account {username}.";
        if (!AuthorityRequester.RequestAuthorityOver(account, Context.Account, authorityReason)) {
            InformSenderClient("You cannot delete an account with a higher auth level than you.");

            return;
        }

        // Yeah, you can't delete your own account.
        if (username == Context.Account.Username) {
            InformSenderClient("You cannot delete your own account.");

            return;
        }

        AccountCollection.DeleteAccount(username);
        InformSenderClient("Account deleted successfully.");
    }

    [Command("lock")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void LockAccountCommand(string username) {
        Reply(AccountAdministration.SetLocked(Context.Account, Context.Character, username, true));
    }

    [Command("unlock")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void UnlockAccountCommand(string username) {
        Reply(AccountAdministration.SetLocked(Context.Account, Context.Character, username, false));
    }

    [Command("password")]
    [AuthRequired(AuthLevel.Administrator)]
    private void ChangePasswordCommand(string username, [Sensitive] string newPassword, [Sensitive] string newPasswordConfirm) {
        if (newPassword != newPasswordConfirm) {
            InformSenderClient("Passwords do not match.");

            return;
        }

        Reply(AccountAdministration.ChangePassword(Context.Account, Context.Character, username, newPassword));
    }

    [Command("authlevel")]
    [AuthRequired(AuthLevel.Administrator)]
    private void ChangeAuthLevelCommand(string username, string authLevel) {
        Reply(AccountAdministration.SetAuthLevel(Context.Account, Context.Character, username, authLevel));
    }

    [Command("info")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void GetAccountInfoCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");

            return;
        }

        // Craft the reply.
        var sb = new StringBuilder();
        sb.Append($"<center>{account.Username}</center>\n");
        sb.Append($"<center>Auth Level: {account.AuthLevel}</center>\n");
        sb.AppendLine("");

        sb.Append($"<left>Creation Time: {account.CreationTime}</left>\n");
        sb.Append($"<left>Last Login Time: {account.LastLoginTime}</left>\n");
        sb.Append($"<left>Last Login Machine ID: {account.LastLoginMachineId}</left>\n");
        sb.Append($"<left>Last Login IP: {account.LastLoginIp}</left>\n");
        sb.AppendLine("");

        var history = account.InfractionHistory;
        sb.Append($"<left>Is Locked: {account.IsLocked}</left>\n");

        if (history.IsCurrentlyBanned) {
            sb.Append($"<left>Ban Ends At: {history.BanEndsAt}</left>\n");
        }

        if (history.IsCurrentlyMuted) {
            sb.Append($"<left>Mute Ends At: {history.MuteEndsAt}</left>\n");
        }

        if (history.Infractions.Count > 1) {
            sb.Append($"<left>Infractions: {history.Infractions.Count}</left>\n");
        }
        else {
            sb.Append($"<left>No infractions.</left>\n");
        }

        sb.Append($"<left>Character IDs:");
        for (int i = 0; i < account.CharacterIds.Count; i++) {
            sb.Append($"{account.CharacterIds[i]}, ");
        }

        InformSenderClient(sb.ToString(), true);
    }

    [Command("infractions")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("warns", "warnings")]
    private void GetAccountInfractionsCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");

            return;
        }

        var history = account.InfractionHistory;
        if (history.Infractions.Count >= 1) {
            var sb = new StringBuilder();
            sb.Append($"<left>Infractions:</left>\n");
            for (int i = 0; i < history.Infractions.Count; i++) {
                var infraction = history.Infractions[i];
                sb.Append($"<left>[{i + 1}] {infraction.InfractionTime} ({infraction.ResponsibleModerator}): {infraction.Reason}</left>\n");
            }

            InformSenderClient(sb.ToString(), true);
        }
        else {
            InformSenderClient("No infractions.");
        }
    }

    [Command("warn")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void WarnCommand(string username, [Remainder] string reason) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");

            return;
        }

        var authorityReason = $"{Context.Account.Username} wants to warn account {username}.";
        if (!AuthorityRequester.RequestAuthorityOver(account, Context.Account, authorityReason)) {
            InformSenderClient("You cannot warn an account with a higher auth level than you.");

            return;
        }

        var infractionAddedSuccess = account.AddInfraction(InfractionType.Warn, reason, Context.Account.Username);

        var reply = infractionAddedSuccess is not null ? "Infraction added successfully." : "Infraction add failed.";
        InformSenderClient(reply);
    }

    [Command("removewarn")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void RemoveWarnCommand(string username, string infractionIndex) {
        // Parse the infraction index as an integer.
        if (!int.TryParse(infractionIndex, out var infractionIndexInt)) {
            InformSenderClient("Invalid infraction index.");

            return;
        }

        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");

            return;
        }

        var authorityReason = $"{Context.Account.Username} wants to remove a warning from account {username}.";
        if (!AuthorityRequester.RequestAuthorityOver(account, Context.Account, authorityReason)) {
            InformSenderClient("You cannot remove a warning from an account with a higher auth level than you.");

            return;
        }

        var infractionRemovedSuccess = account.RemoveInfraction(infractionIndexInt - 1);

        var reply = infractionRemovedSuccess ? "Infraction removed successfully." : "Infraction remove failed.";
        InformSenderClient(reply);
    }

    private void Reply(AdminActionResult result) => InformSenderClient(result.Message);

}
