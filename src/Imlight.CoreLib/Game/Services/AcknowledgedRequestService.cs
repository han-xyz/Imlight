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
 * ACKNOWLEDGED REQUEST SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Accepts the client's telemetry and retail-service requests the server has no state for, answering the
 * ones whose protocol has a reply with an empty one, so they stop logging as unhandled.
 * 
 * USAGE EXAMPLE:
 * Internal service attached to SessionActor; the client sends these around login and on its own timers.
 * 
 * NOTE:
 * Each request type is logged at Debug once per session.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

internal sealed class AcknowledgedRequestService(SessionActor sessionActor) : MessageService(sessionActor) {

    private readonly HashSet<Type> _loggedRequests = [];

    internal static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new AcknowledgedRequestService(parentActor));

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_CONNECTIONSTATS))]
    private void ReceiveConnectionStats(WIZARD2_53_PROTOCOL.MSG_CONNECTIONSTATS message) => Acknowledge(message);

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_LOGCLIENTRESOLUTION))]
    private void ReceiveClientResolution(WIZARD_12_PROTOCOL.MSG_LOGCLIENTRESOLUTION message) => Acknowledge(message);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_LOGVIDEOCAPABILITIES))]
    private void ReceiveVideoCapabilities(WIZARD2_53_PROTOCOL.MSG_LOGVIDEOCAPABILITIES message) => Acknowledge(message);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_PERIODICSUBSCRIPTIONS))]
    private void ReceivePeriodicSubscriptions(WIZARD2_53_PROTOCOL.MSG_PERIODICSUBSCRIPTIONS message) => Acknowledge(message);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTCROWNSREWARDSEVENTS))]
    private void ReceiveCrownsRewardsEventsRequest(WIZARD2_53_PROTOCOL.MSG_REQUESTCROWNSREWARDSEVENTS message)
        => Acknowledge(message);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_REQUESTMAINLINEAUDIT))]
    private void ReceiveMainlineAuditRequest(WIZARD2_53_PROTOCOL.MSG_REQUESTMAINLINEAUDIT message) => Acknowledge(message);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_SplashEnabled))]
    private void ReceiveSplashEnabled(WIZARD2_53_PROTOCOL.MSG_SplashEnabled message) => Acknowledge(message);

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_GETSUBSCRIBERONLYITEMS))]
    private void ReceiveSubscriberOnlyItemsRequest(WIZARD_12_PROTOCOL.MSG_GETSUBSCRIBERONLYITEMS message) {
        Acknowledge(message);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_SUBSCRIBERONLYITEMS { Data = new ByteString() });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_GETTIMEDACCESSPASSES))]
    private void ReceiveTimedAccessPassesRequest(WIZARD_12_PROTOCOL.MSG_GETTIMEDACCESSPASSES message) {
        Acknowledge(message);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_TIMEDACCESSPASSES { Data = new ByteString() });
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_LOYALTYPROGRAMFORDAILYSPIRAL))]
    private void ReceiveLoyaltyProgramRequest(WIZARD3_56_PROTOCOL.MSG_LOYALTYPROGRAMFORDAILYSPIRAL message) {
        Acknowledge(message);
        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_LOYALTYPROGRAMFORDAILYSPIRAL {
            LoyaltyProgramFlag = 0,
            LoyaltyNumTokensCanClaim = 0
        });
    }

    private void Acknowledge(IMessage request) {
        if (_loggedRequests.Add(request.GetType())) {
            Logger.Debug("Session {SessionId} sent {Request}; accepted with nothing to report.",
                Logger.Args(SessionActor.SessionID, request.GetType().Name));
        }
    }

}
