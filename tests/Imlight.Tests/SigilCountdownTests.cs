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
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Packets;
using Xunit;

namespace Imlight.Tests;

public sealed class SigilCountdownTests {

    private const ulong CharId = 7;
    private const string Destination = "WizardCity/WC_Dungeon";

    [Fact]
    public void Start_DungeonSigil_WaitsInTheRunBookAndStartsTheTimerOnce() {
        var entry = new ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY { DestinationZone = Destination };
        List<(ulong, string)> waits = [];
        List<ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY> timers = [];

        SigilCountdown.Start(entry, CharId, (charId, zone) => waits.Add((charId, zone)), timers.Add);

        Assert.Equal([(CharId, Destination)], waits);
        Assert.Equal([entry], timers);
    }

    [Fact]
    public void Start_MinigameSigil_SkipsTheRunBookAndStartsTheTimerOnce() {
        var entry = new ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY { DestinationZone = Destination, SigilRef = ActorRefs.Nobody };
        List<(ulong, string)> waits = [];
        List<ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY> timers = [];

        SigilCountdown.Start(entry, CharId, (charId, zone) => waits.Add((charId, zone)), timers.Add);

        Assert.Empty(waits);
        Assert.Equal([entry], timers);
    }

}
