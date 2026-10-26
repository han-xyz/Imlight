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
 * Asks the session to have the NPC a ResReInteract names re-send its options.
 * 
 * USAGE EXAMPLE:
 * Runs from a quest or goal result list.
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

internal sealed class ResReInteractHandler : BaseResultHandler<ResReInteract> {

    public override bool Execute(IResultContext context) {
        // The session batches this with the refresh its own quest change sends, and holds it while a dialog is open.
        context.GetPlayerRef().Tell(new CHARACTER_103_PROTOCOL.MSG_REQUESTNPCREFRESH {
            PersonaName = Result.m_personaName,
        }, ActorRefs.NoSender);

        return true;
    }

}
