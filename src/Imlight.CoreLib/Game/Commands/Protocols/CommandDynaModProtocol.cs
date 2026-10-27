/*
 * Imlight
 * Copyright (C) 2026 Revive101
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

internal class CommandDynaMod : CommandProtocol {

    internal override string Group { get; set; } = "dynamod";

    [Command("list")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ListDynaModsCommand() => Inform(CharacterActions.ListDynamods(OwnCharacter));

    [Command("set")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetDynaModCommand(string state, [Remainder] string clientTag)
        => Inform(CharacterActions.SetDynamod(OwnCharacter, state, clientTag));

    [Command("toggle")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ToggleDynaModCommand([Remainder] string clientTag)
        => Inform(CharacterActions.ToggleDynamod(OwnCharacter, clientTag));

    [Command("remove")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void RemoveDynaModCommand([Remainder] string clientTag)
        => Inform(CharacterActions.RemoveDynamod(OwnCharacter, clientTag));

}
