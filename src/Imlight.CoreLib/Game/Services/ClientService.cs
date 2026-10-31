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
 * CLIENT SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages client connection lifecycle, handling disconnection and logout 
 * processes for game server sessions.
 * 
 * USAGE EXAMPLE:
 * Internal service for managing client connection state and graceful 
 * disconnection mechanisms.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

internal class ClientService(SessionActor sessionActor) : MessageService(sessionActor) {
    
    private const int RequirementCheckReportInterval = 100;

    private static readonly TimeSpan s_instanceQueryTimeout = TimeSpan.FromSeconds(2);

    private readonly HashSet<string> _askedRequirements = new(StringComparer.Ordinal);
    private int _requirementChecks;

    protected static Props Props(SessionActor parentActor) 
        => Akka.Actor.Props.Create(() => new ClientService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect() {
        Logger.Debug("Session {SessionId} asked to disconnect.", Logger.Args(SessionActor.SessionID));

        SendToSocket(new GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT());

        Logger.Debug("Session {SessionId} disconnect confirmed.", Logger.Args(SessionActor.SessionID));

        // The client closes its end of the connection after the echo, so the server does not close first.
        CloseSessionAfterClient();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message) {
        Logger.Debug("Session {SessionId} asked to log out.", Logger.Args(SessionActor.SessionID));

        var isInstance = QueryIsInstance(SessionActor.GetZoneActor(), SessionActor.SessionID);
        SendToSocket(new GAME_5_PROTOCOL.MSG_QUERY_LOGOUT { IsInstance = isInstance });

        Logger.Debug("Session {SessionId} logout answered (in an instance: {IsInstance}).",
            Logger.Args(SessionActor.SessionID, isInstance));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQASKSERVER))]
    private void ReceiveReqServer(GAME_5_PROTOCOL.MSG_REQASKSERVER message) {
        // todo: answer these. What does the client expect in Response? Answering 1 for a met requirement, with or
        // without m_applyNOT applied, showed new wizards the quest notifications of later worlds.
        var serialized = (byte[]) message.Requirement ?? [];
        _requirementChecks++;
        if (_askedRequirements.Add(Convert.ToHexString(serialized))) {
            var requirement = RequirementQuery.TryDecode(serialized, out var decoded)
                ? decoded.ToString()
                : $"an unreadable requirement ({serialized.Length}B)";
            Logger.Debug("Session {SessionId} asked the server to check {Requirement}.",
                Logger.Args(SessionActor.SessionID, requirement));
        }

        if (_requirementChecks % RequirementCheckReportInterval == 0) {
            Logger.Debug("Session {SessionId} has asked for {Count} requirement checks, {Distinct} of them different.",
                Logger.Args(SessionActor.SessionID, _requirementChecks, _askedRequirements.Count));
        }
    }

    private static byte QueryIsInstance(IActorRef zoneActor, ushort sessionId) {
        if (zoneActor is null) {
            return 0;
        }

        try {
            var rsp = zoneActor.Ask<ZONE_102_PROTOCOL.MSG_QUERYINSTANCEQUESTSRSP>(
                new ZONE_102_PROTOCOL.MSG_QUERYINSTANCEQUESTS(), s_instanceQueryTimeout).Result;

            return (byte) (rsp.IsInstance ? 1 : 0);
        }
        catch (Exception ex) {
            Logger.Warning("Session {SessionId} could not ask its zone whether it is an instance, so the logout treats it as open world. {Exception}", Logger.Args(sessionId, ex));

            return 0;
        }
    }

}
