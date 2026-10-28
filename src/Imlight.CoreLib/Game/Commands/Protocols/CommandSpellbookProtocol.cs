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

internal class CommandSpellbookProtocol : CommandProtocol {

    internal override string Group { get; set; } = "sb";

    [Command("learn")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void LearnSpellCommand(string spellTemplateId)
        => Inform(CharacterActions.LearnSpell(OwnCharacter, spellTemplateId));

    [Command("unlearn")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void UnlearnSpellCommand(string spellTemplateId)
        => Inform(CharacterActions.UnlearnSpell(OwnCharacter, spellTemplateId));

    [Command("unlearnall")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void UnlearnallSpellCommand() => Inform(CharacterActions.UnlearnAllSpells(OwnCharacter));

    [Command("add")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddSpellCommand(string spellTemplateId)
        => Inform(CharacterActions.AddTemporarySpell(OwnCharacter, spellTemplateId));

    [Command("remove")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void RemoveSpellCommand(string spellTemplateId)
        => Inform(CharacterActions.RemoveTemporarySpell(OwnCharacter, spellTemplateId));

    [Command("allcantrips")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void LearnallCantripsCommand() => Inform(CharacterActions.LearnAllCantrips(OwnCharacter));

}
