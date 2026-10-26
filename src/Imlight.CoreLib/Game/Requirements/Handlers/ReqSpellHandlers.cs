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
 * REQUIREMENT HANDLERS
 * ========================================================================
 * 
 * PURPOSE:
 * Evaluates the requirements on the spells a wizard has trained.
 * 
 * USAGE EXAMPLE:
 * Registered by RequirementDispatcher; trainers send ReqHasSpell for a spell's prerequisite.
 * 
 * NOTE:
 * Treasure cards and item spells are not trained spells and are not read.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal static class TrainedSpells {

    public static IEnumerable<SpellTemplate> Of(Wizard wizard)
        => wizard?.SpellbookBehavior?.LearnedSpellTemplateIds
            .Select(id => CoreObjectFactory.GetCoreTemplate(id) as SpellTemplate)
            .Where(template => template is not null)
        ?? [];

    public static bool NameMatches(string name, string wanted)
        => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase);

}

/// <summary>
/// ReqHasSpell: the player has trained the spell. A requirement without m_hasExactSpellName also accepts a
/// spell whose base spell has the name.
/// </summary>
internal sealed class ReqHasSpellHandler : BaseRequirementHandler<ReqHasSpell> {

    public override bool Evaluate(IRequirementContext context) {
        string wanted = Requirement.m_spellName;
        if (string.IsNullOrEmpty(wanted)) {
            return false;
        }

        return TrainedSpells.Of(context.GetWizard()).Any(spell =>
            TrainedSpells.NameMatches(spell.m_name, wanted)
            || !Requirement.m_hasExactSpellName && TrainedSpells.NameMatches(spell.m_spellBase, wanted));
    }

}

/// <summary>
/// ReqHasSecondarySchool: the player has trained a spell of the school, and it is not the player's own school.
/// </summary>
internal sealed class ReqHasSecondarySchoolHandler : BaseRequirementHandler<ReqHasSecondarySchool> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        string school = Requirement.m_secondarySchool;
        if (wizard?.MagicSchoolBehavior is null || string.IsNullOrEmpty(school)
            || TrainedSpells.NameMatches(wizard.MagicSchoolBehavior.MagicSchool.ToString(), school)) {
            return false;
        }

        return TrainedSpells.Of(wizard).Any(spell => TrainedSpells.NameMatches(spell.m_sMagicSchoolName, school));
    }

}
