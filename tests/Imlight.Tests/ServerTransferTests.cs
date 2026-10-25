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
 * SERVER TRANSFER TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for sending MSG_SERVERTRANSFER together with its transfer ticket.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Packets;
using Xunit;

namespace Imlight.Tests;

public class ServerTransferTests {

    private const ulong UserId = 42;
    private const string RealmName = "Imlight";
    private const string RemoteIp = "192.168.10.121";
    private const string ZoneName = "WizardCity/Interiors/WC_Headmistress_House";

    [Fact]
    public void Send_IssuesTicketForTheZoneItSends() {
        var store = new SessionKeyStore();
        GAME_5_PROTOCOL.MSG_SERVERTRANSFER sent = null;

        var isSent = ServerTransfer.Send(UserId, RealmName, RemoteIp, ZoneName,
            request => {
                store.IssueTransferTicket(request.UserId, request.RemoteIp, request.ZoneName);
                return new SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP { Success = true };
            },
            _ => new GAME_5_PROTOCOL.MSG_SERVERTRANSFER { ZoneName = "WizardCity/WC_Hub" },
            transfer => sent = transfer);

        Assert.True(isSent);
        Assert.Equal(UserId, sent.UserID);
        Assert.Equal(SessionKeyStore.AttachVerdict.TransferTicket,
            store.Validate(sent.UserID, [], RemoteIp, (string) sent.ZoneName));
    }

    [Fact]
    public void Send_TicketRefused_SendsNothing() {
        GAME_5_PROTOCOL.MSG_SERVERTRANSFER sent = null;

        var isSent = ServerTransfer.Send(UserId, RealmName, RemoteIp, ZoneName,
            _ => new SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP { Success = false },
            _ => new GAME_5_PROTOCOL.MSG_SERVERTRANSFER(),
            transfer => sent = transfer);

        Assert.False(isSent);
        Assert.Null(sent);
    }

}
