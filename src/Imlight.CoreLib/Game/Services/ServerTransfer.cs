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
 * SERVER TRANSFER
 * ========================================================================
 * 
 * PURPOSE:
 * Sends MSG_SERVERTRANSFER only after the target realm has issued a transfer ticket for the
 * same user, source address and zone, so the client's empty-key attach that follows is accepted.
 * 
 * USAGE EXAMPLE:
 * SendServerTransfer(userId, realmName, zoneName, ticket => new GAME_5_PROTOCOL.MSG_SERVERTRANSFER { ... });
 * 
 * NOTE:
 * Send sets UserID and ZoneName on the built message, so the ticket and the transfer cannot disagree.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

internal static class ServerTransfer {

    public static bool Send(ulong userId, string targetRealmName, string remoteIp, string zoneName,
        Func<SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEY, SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP> issueTicket,
        Func<SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP, GAME_5_PROTOCOL.MSG_SERVERTRANSFER> build,
        Action<GAME_5_PROTOCOL.MSG_SERVERTRANSFER> send) {
        var ticket = issueTicket(new SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEY {
            UserId = userId,
            TargetRealmName = targetRealmName,
            RemoteIp = remoteIp,
            ZoneName = zoneName
        });
        if (ticket is null || !ticket.Success) {
            return false;
        }

        var transfer = build(ticket);
        transfer.UserID = userId;
        transfer.ZoneName = zoneName;
        send(transfer);

        return true;
    }

}
