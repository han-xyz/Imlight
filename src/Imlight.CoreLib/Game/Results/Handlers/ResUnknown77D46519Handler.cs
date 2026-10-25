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
 * Accepts the client's activity-tracking result (hash 0x77D46519) so the
 * rest of a result list runs; the server has nothing to record for it.
 * 
 * USAGE EXAMPLE:
 * Runs from a generic chest's interact result list, before ResPlaySound.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResUnknown77D46519Handler : BaseResultHandler<ResUnknown77D46519> {

    public override bool Execute(IResultContext context) {
        // m_activityType is the client's activity category for the interaction (25 on wooden chests).
        Logger.Debug("Activity result with activity type {ActivityType}.", Logger.Args(Result.m_activityType));

        return true;
    }

}
