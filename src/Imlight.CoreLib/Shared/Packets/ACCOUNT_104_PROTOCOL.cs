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

using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class ACCOUNT_104_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 104;
    public string ProtocolType { get; } = "ACCOUNT";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Server Account Messages.";

    public class MSG_QUERYACCOUNT : IServerMessage {
        
        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 104;

    }

    public class MSG_ACCOUNT : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 104;

        public Account Account;

    }

    /// <summary>
    /// Asks an online player's session to add a signed amount to its account's Crowns, so the change cannot race
    /// a Crown Shop purchase. The session answers with <see cref="MSG_CROWNSADJUSTED"/>.
    /// </summary>
    public class MSG_ADJUSTCROWNS : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 104;

        public int Change;

    }

    public class MSG_CROWNSADJUSTED : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 104;

        public bool Applied;
        public int Before;
        public int After;

    }

    /// <summary>
    /// Tells an online player's session that staff changed the stored account, so the session reloads what it
    /// checks while the player plays: infractions (mutes), the auth level and the lock.
    /// </summary>
    public class MSG_ACCOUNTCHANGED : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 104;

    }

}
