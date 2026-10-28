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

using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandAccountProtocol : CommandProtocol {

    internal override string Group { get; set; } = "account";

    [Command("create")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void CreateAccountCommand(string username, string password) {
        Reply(AccountAdministration.CreateAccount(Context.Account, Context.Character, username, password));
    }

    [Command("delete")]
    [AuthRequired(AuthLevel.Administrator)]
    private void DeleteAccountCommand(string username)
        => Inform(AccountModeration.DeleteAccount(Context.Account, Context.Character, Context.Actors, username));

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
    private void ChangePasswordCommand(string username, string newPassword, string newPasswordConfirm) {
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
    private void GetAccountInfoCommand(string username) => Inform(AccountModeration.Info(Context.Account, username), true);

    [Command("infractions")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("warns", "warnings")]
    private void GetAccountInfractionsCommand(string username)
        => Inform(AccountModeration.Infractions(Context.Account, username), true);

    [Command("warn")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void WarnCommand(string username, [Remainder] string reason)
        => Inform(AccountModeration.Warn(Context.Account, Context.Character, username, reason));

    [Command("removewarn")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void RemoveWarnCommand(string username, string infractionIndex)
        => Inform(AccountModeration.RemoveInfraction(Context.Account, Context.Character, username, infractionIndex));

    private void Reply(AdminActionResult result) => InformSenderClient(result.Message);

}
