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
 * Evaluates ReqMagicLevel: the wizard's level against the requirement's number.
 * 
 * USAGE EXAMPLE:
 * Registered by RequirementDispatcher for quests and triggers gated on a level.
 * 
 * NOTE:
 * m_magicSchool is not read: the server keeps a single level per wizard.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqMagicLevelHandler : BaseRequirementHandler<ReqMagicLevel> {

    public override bool Evaluate(IRequirementContext context) {
        var schoolBehavior = context.GetWizard()?.MagicSchoolBehavior;
        if (schoolBehavior is null) {
            return false;
        }

        return NumericRequirement.Compare(Requirement, schoolBehavior.Level);
    }

}
