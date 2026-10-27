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
 *
 * ========================================================================
 * HATCH COMMAND
 * ========================================================================
 *
 * PURPOSE:
 * Quality assurance command that hatches two of the wizard's own pets without the hatchery: it lists
 * the pets that can be parents, or lays the egg of two of them.
 *
 * USAGE EXAMPLE:
 * hatch
 * hatch 1 2
 *
 * NOTE:
 * A pet is named by its number in the list, its global id or its name without spaces. No gold is taken.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandHatchProtocol : CommandProtocol {

    internal override string Group { get; set; } = "";

    [Command("hatch")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void HatchCommand([Remainder] string parents = "") => Inform(CharacterActions.Hatch(OwnCharacter, parents));

}
