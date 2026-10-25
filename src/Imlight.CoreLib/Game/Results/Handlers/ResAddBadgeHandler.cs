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
 * Accepts a badge grant; the wizard has no badge storage yet, so the
 * grant is logged and the rest of the result list runs.
 * 
 * USAGE EXAMPLE:
 * Runs from a quest's end results (MB-BARK-C06-001).
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

internal sealed class ResAddBadgeHandler : BaseResultHandler<ResAddBadge> {

    public override bool Execute(IResultContext context) {
        Logger.Information("Badge {BadgeName} not granted: badges are not stored on the wizard.",
            Logger.Args(Result.m_badgeName));

        return true;
    }

}
