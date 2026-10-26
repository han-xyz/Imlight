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
 * RESULT HANDLERS
 * ========================================================================
 * 
 * PURPOSE:
 * Grants the treasure card a result names to the wizard's treasure book,
 * through the same path as a dropped or bought card.
 * 
 * USAGE EXAMPLE:
 * Runs from quest end results (WC-TRITON-SIDE-004, WC-MISC-C05-003).
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResAddTreasureSpellHandler : BaseResultHandler<ResAddTreasureSpell> {

    // ResAddTreasureSpell carries no count; it grants a single card.
    private const int CardsPerResult = 1;

    public override bool Execute(IResultContext context) {
        var spellName = Result.m_spellName;
        if (string.IsNullOrEmpty(spellName) || SpellFactory.GetSpell(spellName) is not Spell card) {
            Logger.Warning("Treasure card {SpellName} not found; nothing granted.", Logger.Args(spellName));

            return true;
        }

        if (ActiveWizardQuery.Query(context.GetPlayerRef()) is not Wizard wizard) {
            Logger.Warning("ResAddTreasureSpell could not retrieve the player's wizard.");

            return false;
        }

        LootGranter.GrantTreasureCards(context.GetPlayerRef(), wizard,
            SpellFactory.s_spellTemplates[card.m_spellID], card.m_templateID, CardsPerResult);

        return true;
    }

}
