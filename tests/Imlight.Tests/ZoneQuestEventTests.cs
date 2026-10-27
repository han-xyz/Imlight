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
 * ZONE QUEST EVENT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the quest events a zone hands back to its players
 * when one of them posts an event.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * A session skips the service that sent it a message, so an event a quest
 * service posts must come back from the zone. The event names are
 * GH-Explorer-C04-001's rune-stone generic events.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.IO;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class ZoneQuestEventTests : IDisposable {

    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    private readonly ActorSystem _system = ActorSystem.Create("zone-quest-event-tests");

    public ZoneQuestEventTests() => InitializeQuietLogging();

    public void Dispose() => _system.Terminate().Wait(s_timeout);

    [Theory]
    [InlineData("GH2-RuneStone01")]
    [InlineData("GH2-RuneStone06")]
    public void PostedEvent_FromQuestService_ReachesPlayerFromTheZone(string eventName) {
        var zone = _system.ActorOf(Props.Create(() => new Zone("Grizzleheim/GH_Library", 1, false, null)));
        var received = Inbox.Create(_system);
        var player = _system.ActorOf(Props.Create(() => new RecordingSession(received.Receiver)));
        var questService = Inbox.Create(_system);
        zone.Tell(new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = "test" });
        zone.Tell(new ZONE_102_PROTOCOL.MSG_ADDPLAYER {
            PlayerActor = player,
            Wizard = new Wizard { CharId = 1, QuestBehavior = new ServerQuestBehavior() },
        }, player);

        zone.Tell(new ZONE_102_PROTOCOL.MSG_POSTEVENT { EventName = eventName, PlayerActor = player }, questService.Receiver);

        var (questEvent, sender) = ReceiveQuestEvent(received);
        Assert.Equal(eventName, questEvent.EventName);
        Assert.NotEqual(questService.Receiver, sender);
    }

    private static (ZONE_102_PROTOCOL.MSG_ZONEEVENTFORQUESTS Event, IActorRef Sender) ReceiveQuestEvent(Inbox received) {
        while (true) {
            var (message, sender) = ((object, IActorRef)) received.Receive(s_timeout);
            if (message is ZONE_102_PROTOCOL.MSG_ZONEEVENTFORQUESTS questEvent) {
                return (questEvent, sender);
            }
        }
    }

    private static void InitializeQuietLogging() {
        var iniFilePath = Path.GetTempFileName();
        var logPath = Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt");
        try {
            File.WriteAllText(iniFilePath, $"[Logging]\nLogPath = {logPath}\nLogLevel = ERROR\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    private sealed class RecordingSession : ReceiveActor {

        public RecordingSession(IActorRef received)
            => ReceiveAny(message => received.Tell((message, Sender)));

    }

}
