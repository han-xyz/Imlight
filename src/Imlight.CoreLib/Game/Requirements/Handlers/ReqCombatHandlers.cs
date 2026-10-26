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
 * Evaluates the combat requirements: in a duel, in a PvP duel, and the pip counts.
 * 
 * USAGE EXAMPLE:
 * Registered by RequirementDispatcher for triggers and spell conditions gated on combat.
 * 
 * NOTE:
 * No requirement context carries a duel, so the pip counts always fail.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.PvP;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqInCombat: the player is in a duel.
/// </summary>
internal sealed class ReqInCombatHandler : BaseRequirementHandler<ReqInCombat> {

    public override bool Evaluate(IRequirementContext context) => context.GetWizard()?.IsInDuel == true;

}

/// <summary>
/// ReqPvPCombat: the player is in the duel of a PvP match.
/// </summary>
internal sealed class ReqPvPCombatHandler : BaseRequirementHandler<ReqPvPCombat> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();

        return wizard is { IsInDuel: true } && PvPRegistry.IsInMatch(wizard.CharId);
    }

}

/// <summary>
/// ReqPipCount: the pips of a duel participant; fails outside a duel state.
/// </summary>
internal sealed class ReqPipCountHandler : BaseRequirementHandler<ReqPipCount> {

    public override bool Evaluate(IRequirementContext context) => false;

}

/// <summary>
/// ReqShadowPipCount: the shadow pips of a duel participant; fails outside a duel state.
/// </summary>
internal sealed class ReqShadowPipCountHandler : BaseRequirementHandler<ReqShadowPipCount> {

    public override bool Evaluate(IRequirementContext context) => false;

}
