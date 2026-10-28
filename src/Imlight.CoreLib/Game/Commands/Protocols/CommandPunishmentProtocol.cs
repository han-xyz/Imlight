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

/// <summary>
/// Commands on the account of the player the issuer selected by opening their character window.
/// </summary>
internal class CommandPunishmentProtocol : CommandProtocol {

    internal override string Group { get; set; } = "";

    [Command("mute")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void MuteCommand(string time, [Remainder] string reason) {
        if (TryGetSelectedUsername(out var username)) {
            Inform(AccountModeration.Mute(Context.Account, Context.Character, Context.Actors, username, time, reason), true);
        }
    }

    [Command("unmute")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void UnmuteCommand() {
        if (TryGetSelectedUsername(out var username)) {
            Inform(AccountModeration.Unmute(Context.Account, Context.Character, Context.Actors, username), true);
        }
    }

    [Command("kick")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void KickCommand([Remainder] string reason) {
        if (TryGetSelectedUsername(out var username)) {
            Inform(AccountModeration.Kick(Context.Account, Context.Character, Context.Actors, username, reason), true);
        }
    }

    [Command("warn")]
    [AuthRequired(AuthLevel.HallMonitor)]
    public void WarnCommand([Remainder] string reason) {
        if (TryGetSelectedUsername(out var username)) {
            Inform(AccountModeration.Warn(Context.Account, Context.Character, username, reason), true);
        }
    }

    [Command("info")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void GetAccountInfoCommand() {
        if (TryGetSelectedUsername(out var username)) {
            Inform(AccountModeration.Info(Context.Account, username), true);
        }
    }

    private bool TryGetSelectedUsername(out string username) {
        username = Context.SelectedAccount?.Username;
        if (username is null) {
            InformSenderClient("You must select a user to use this command.", true);
        }

        return username is not null;
    }

}
