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
 * PET RECORD RULES
 * ========================================================================
 * 
 * PURPOSE:
 * Decides when a pet's WizardPets record is dropped because the pet left the character.
 * 
 * USAGE EXAMPLE:
 * if (PetRecordRules.DropsRecordOnMove(from, to)) { WizardPetCollection.Delete(charId, petId); }
 * 
 * NOTE:
 * - Discarding and selling always drop the record; a move between the character's own containers keeps it.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using Imlight.CoreLib.Game.Bank;

namespace Imlight.CoreLib.Game.Pet;

/// <summary>
/// Decides when a pet's WizardPets record is dropped because the pet left the character.
/// </summary>
internal static class PetRecordRules {

    /// <summary>
    /// Tells whether a bank move takes a pet away from the character.
    /// </summary>
    /// <param name="from">Where the pet was.</param>
    /// <param name="to">Where the pet goes.</param>
    /// <returns>True for a deposit into the account's shared bank, where another character can take it.</returns>
    internal static bool DropsRecordOnMove(BankContainer from, BankContainer to)
        => to == BankContainer.SharedBank && from != BankContainer.SharedBank;

}
