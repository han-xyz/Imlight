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
/// Lets staff accounts switch their privileges on for the current session. Every change is audited.
/// </summary>
internal class CommandAdminProtocol : CommandProtocol {

    internal override string Group { get; set; } = "admin";

    // Requiring admin mode to enable it would make it impossible to enable, so these run without it.
    [Command("on")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [WithoutAdminMode]
    private void EnableCommand() {
        var result = AccountAdministration.SetAdminMode(Context.Account, Context.Character, true);
        InformSenderClient(result.Message + " It stays on until you log out or type .admin off.");
    }

    [Command("off")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [WithoutAdminMode]
    private void DisableCommand()
        => InformSenderClient(AccountAdministration.SetAdminMode(Context.Account, Context.Character, false).Message);

    [Command("status")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    [WithoutAdminMode]
    private void StatusCommand() {
        var state = Context.Account.AdminModeEnabled ? "on" : "off";
        InformSenderClient($"Admin mode is {state}. Granted auth level: {Context.Account.AuthLevel}.");
    }

}
