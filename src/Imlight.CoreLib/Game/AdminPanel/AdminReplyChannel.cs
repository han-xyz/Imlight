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
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Sends a session service's command replies to the staff member whose admin panel sent the command, instead of
/// to the player the command acted on.
/// </summary>
internal sealed record AdminReplyChannel(IActorRef ReplyTo, uint RequestId) {

    internal void Send(string text)
        => ReplyTo.Tell(new ADMIN_112_PROTOCOL.MSG_ADMINACTIONREPLY {
            RequestId = RequestId,
            Succeeded = true,
            Message = text
        });

}
