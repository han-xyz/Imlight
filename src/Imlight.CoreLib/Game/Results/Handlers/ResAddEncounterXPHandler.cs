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
 * Grants a result's encounter experience through the session's XP path,
 * which levels the wizard up when the total crosses a level.
 * 
 * USAGE EXAMPLE:
 * Runs from quest end results (MS-DTH3-C01-001).
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResAddEncounterXPHandler : BaseResultHandler<ResAddEncounterXP> {

    public override bool Execute(IResultContext context) {
        if (Result.m_experience == 0) {
            return true;
        }

        context.GetPlayerRef().Tell(new CHARACTER_103_PROTOCOL.MSG_GAINXP { XP = Result.m_experience });

        return true;
    }

}
