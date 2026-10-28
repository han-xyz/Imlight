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
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// The character a staff action works on: live in its own session, or loaded from the database while its player
/// is offline.
/// </summary>
/// <remarks>
/// A live character must be changed by its own session: the session holds the character in memory and saves whole
/// parts of it, so a change written to the database behind its back is lost. An offline character is loaded, and
/// the <see cref="Wizard"/> methods save what they change. Messages for the player's client go to
/// <see cref="Session"/>, which drops them when the character is offline.
/// </remarks>
internal sealed class CharacterTarget {

    private CharacterTarget(Wizard wizard, CoreObject gameObject, IActorRef session, AdminReplyChannel replies) {
        Wizard = wizard;
        GameObject = gameObject;
        Session = session;
        Replies = replies;
    }

    internal Wizard Wizard { get; }

    /// <summary>
    /// The player object in the zone; null when the character is offline.
    /// </summary>
    internal CoreObject GameObject { get; }

    internal IActorRef Session { get; }

    /// <summary>
    /// Where session services send their replies; null sends them to the player, as chat commands do.
    /// </summary>
    internal AdminReplyChannel Replies { get; }

    internal bool Online => GameObject is not null;

    internal static CharacterTarget Live(Wizard wizard, CoreObject gameObject, IActorRef session,
                                         AdminReplyChannel replies = null)
        => new(wizard, gameObject, session, replies);

    internal static CharacterTarget Offline(Wizard wizard) => new(wizard, null, ActorRefs.Nobody, null);

    /// <summary>
    /// Tells the character's session: a client message goes to the player, an internal one to its services.
    /// </summary>
    internal void Tell(object message) => Session.Tell(message, ActorRefs.NoSender);

}
