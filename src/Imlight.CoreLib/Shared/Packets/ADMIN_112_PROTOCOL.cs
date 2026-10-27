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

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Packets;

/// <summary>
/// Internal messages of the admin panel's character actions.
/// </summary>
public sealed class ADMIN_112_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 112;
    public string ProtocolType { get; } = "ADMIN";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Admin Panel Messages.";

    /// <summary>
    /// Sent by a staff member's admin panel to the session of an online character: run a panel action on that
    /// session's own character. The session answers <see cref="ReplyTo"/> with <see cref="MSG_ADMINACTIONREPLY"/>.
    /// </summary>
    public sealed class MSG_ADMINCHARACTERACTION : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 112;

        public uint RequestId;

        /// <summary>
        /// The character the action is for; a session whose active character differs refuses it.
        /// </summary>
        public ulong CharId;

        public string Action;
        public string[] Arguments;

        /// <summary>
        /// The staff member's account and character, for the audit log.
        /// </summary>
        public Account Actor;
        public Wizard ActorCharacter;

        /// <summary>
        /// The staff member's session.
        /// </summary>
        public IActorRef ReplyTo;

    }

    /// <summary>
    /// The outcome of an admin panel action that ran in another session, or a reply a session service sent while
    /// carrying one out.
    /// </summary>
    public sealed class MSG_ADMINACTIONREPLY : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 112;

        public uint RequestId;
        public ulong CharId;
        public bool Succeeded;
        public string Message;
        public List<string> Lines;

        /// <summary>
        /// True when the reply is the action's own outcome rather than a session service's message.
        /// </summary>
        public bool Final;

        /// <summary>
        /// The character as it now is, for the panel's summary and head; null for a service's message.
        /// </summary>
        public List<string> Summary;
        public int Level;

    }

    /// <summary>
    /// Fired in the staff member's session when a session has not answered a request in time.
    /// </summary>
    public sealed class MSG_ADMINREQUESTTIMEOUT : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 112;

        public uint RequestId;

    }

}
