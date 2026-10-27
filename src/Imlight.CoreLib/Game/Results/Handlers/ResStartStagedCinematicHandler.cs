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
 * START STAGED CINEMATIC RESULT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Plays a staged cinematic (a cutscene the client has in its data, started by name and stage) for the
 * player a trigger or quest fired it for, or for every player in the zone when the result says so.
 *
 * USAGE EXAMPLE:
 * The result executor runs it for every ResStartStagedCinematic of a fired trigger or quest; nothing
 * calls it directly.
 *
 * NOTE:
 * The client is told MSG_STARTSTAGEDCINEMATIC with the player's own place and facing as the cinematic's
 * origin. The zone's triggers that wait on the cinematic's end event are released through the zone at the
 * same time, since the client posts the end event only to the quest side (MSG_POSTZONEEVENTFROMCLIENT).
 *
 * TODO:
 * Post the trigger end events when the client reports the cinematic over instead of at its start.
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResStartStagedCinematicHandler : BaseResultHandler<ResStartStagedCinematic> {

    public override bool Execute(IResultContext context) {
        var playerRef = context.GetPlayerRef();
        if (playerRef is null || Result is null) {
            return false;
        }

        var cinematicName = Result.m_cinematicName?.ToString() ?? "";
        if (cinematicName.Length == 0) {
            Logger.Warning("ResStartStagedCinematic from {Source} names no cinematic, so nothing plays.",
                Logger.Args(context.GetSourceName()));

            return false;
        }

        var playerObj = context.GetPlayerObj();
        var startMessage = new GAME_5_PROTOCOL.MSG_STARTSTAGEDCINEMATIC {
            CinematicName = cinematicName,
            InitialStageName = Result.m_stageName?.ToString() ?? "",
            Start_X = playerObj?.m_location.X ?? 0f,
            Start_Y = playerObj?.m_location.Y ?? 0f,
            Start_Z = playerObj?.m_location.Z ?? 0f,
            Yaw = playerObj?.m_orientation.Z ?? 0f,
            DuelID = 0,
            WinningTeamID = 0
        };

        var zoneActor = context.GetZoneActor();
        if (Result.m_bIncludeAllPlayersInZone && zoneActor is not null) {
            zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
                Message = startMessage,
                Sender = playerRef,
                Selfless = false,
                Targets = ZoneBroadcastTarget.Players
            }, ActorRefs.NoSender);
        }
        else {
            playerRef.Tell(startMessage, ActorRefs.NoSender);
        }

        Logger.Debug("Staged cinematic {Cinematic} stage {Stage} starts for {Scope}.",
            Logger.Args(cinematicName, Result.m_stageName, Result.m_bIncludeAllPlayersInZone ? "the zone" : "the player"));

        // The triggers waiting on the cinematic's end are released by the zone.
        zoneActor?.Tell(new ZONE_102_PROTOCOL.MSG_STARTSTAGEDCINEMATIC {
            PlayerActor = playerRef,
            PlayerGameObject = playerObj,
        }, ActorRefs.NoSender);

        return true;
    }

}
