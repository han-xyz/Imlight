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
 * Accepts an elixir's expansion grant so the rest of its action list runs;
 * every player already owns every expansion on this server.
 * 
 * USAGE EXAMPLE:
 * Runs from an elixir's equip and unequip action lists.
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

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResAddExpansionHandler : BaseResultHandler<ResAddExpansion> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Expansion result for category {Category}; every expansion is already owned.", Logger.Args(Result.m_category));

        return true;
    }

}
