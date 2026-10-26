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
 * BACKPACK EXPANSION REQUIREMENTS
 * ========================================================================
 * 
 * PURPOSE:
 * Answers the requirements about the Expand Backpack Elixir limit.
 * 
 * USAGE EXAMPLE:
 * ReqAddIncreaseInventory is the elixir's purchase requirement; client tutorial tips use
 * ReqIsLimitExpandBackpackElixirs.
 * 
 * NOTE:
 * The limit itself lives in BackpackExpansion.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Elixirs;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqIsLimitExpandBackpackElixirsHandler : BaseRequirementHandler<ReqIsLimitExpandBackpackElixirs> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();

        return wizard?.GameStats is not null && BackpackExpansion.IsAtLimit(wizard);
    }

}

internal sealed class ReqAddIncreaseInventoryHandler : BaseRequirementHandler<ReqAddIncreaseInventory> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();

        return wizard?.GameStats is not null && !BackpackExpansion.IsAtLimit(wizard);
    }

}
