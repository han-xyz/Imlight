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

using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

/// <summary>
/// Runs a staff member's admin panel action on this session's own character, and answers the staff member.
/// </summary>
/// <remarks>
/// A live character must be changed by its own session (see <see cref="CharacterTarget"/>), so the staff member's
/// panel sends the action here. It is a service of its own, not part of <see cref="AdminPanelService"/>, so that a
/// staff member acting on their own character is served by a different actor than the one that sent the action.
/// The staff member's panel has already checked their level and their right to act on this account.
/// </remarks>
internal class AdminActionService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new AdminActionService(parentActor));

    [MessageHandler(typeof(ADMIN_112_PROTOCOL.MSG_ADMINCHARACTERACTION))]
    private void ReceiveCharacterAction(ADMIN_112_PROTOCOL.MSG_ADMINCHARACTERACTION message) {
        var result = Run(message);

        // A deferred action is answered by the service that carries it out.
        message.ReplyTo?.Tell(new ADMIN_112_PROTOCOL.MSG_ADMINACTIONREPLY {
            RequestId = message.RequestId,
            CharId = message.CharId,
            Succeeded = result.Succeeded,
            Message = result.Deferred ? null : result.Message ?? "",
            Lines = result.Lines?.ToList(),
            Final = true
        });
    }

    private AdminActionResult Run(ADMIN_112_PROTOCOL.MSG_ADMINCHARACTERACTION message) {
        var wizard = GetActiveWizard();
        if (wizard is null || wizard.CharId != message.CharId) {
            return AdminActionResult.Refuse("The character has left that session. Refresh and try again.");
        }

        var action = PanelActions.Find(message.Action);
        if (action?.OnCharacter is null) {
            return AdminActionResult.Refuse($"Unknown action '{message.Action}'.");
        }

        var target = CharacterTarget.Live(wizard, GetActiveGameObject(), SessionActor.ActorRef,
            new AdminReplyChannel(message.ReplyTo, message.RequestId));

        return PanelActions.RunOnCharacter(action, target, message.Arguments ?? [], message.Actor,
            message.ActorCharacter);
    }

}
