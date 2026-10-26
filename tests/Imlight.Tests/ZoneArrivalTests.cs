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
 * ZONE ARRIVAL TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for holding the mount reconcile until the client has zoned.
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

using System.Collections.Generic;
using Imlight.CoreLib.Game.World;
using Xunit;

namespace Imlight.Tests;

public class ZoneArrivalTests {

    private const string Reconcile = "mount reconcile";
    private const string ClientZoned = "MSG_CLIENTZONED";

    [Fact]
    public void PlayerAdded_SendsNothingBeforeTheClientZones() {
        var sent = new List<string>();
        var arrival = new ZoneArrival(() => sent.Add(Reconcile));

        arrival.PlayerAdded();

        Assert.Empty(sent);
    }

    [Fact]
    public void ClientZoned_SendsTheReconcileOnceAfterIt() {
        var sent = new List<string>();
        var arrival = new ZoneArrival(() => sent.Add(Reconcile));

        arrival.PlayerAdded();
        sent.Add(ClientZoned);
        arrival.ClientZoned();
        arrival.ClientZoned();

        Assert.Equal([ClientZoned, Reconcile], sent);
    }

    [Fact]
    public void FallbackElapsed_SendsTheReconcileWhenTheClientNeverZones() {
        var sent = new List<string>();
        var arrival = new ZoneArrival(() => sent.Add(Reconcile));

        arrival.PlayerAdded();
        arrival.FallbackElapsed();

        Assert.Equal([Reconcile], sent);
    }

    [Fact]
    public void ClientZonedAfterTheFallback_DoesNotSendAgain() {
        var sent = new List<string>();
        var arrival = new ZoneArrival(() => sent.Add(Reconcile));

        arrival.PlayerAdded();
        arrival.FallbackElapsed();
        arrival.ClientZoned();

        Assert.Equal([Reconcile], sent);
    }

    [Fact]
    public void FallbackAfterTheClientZoned_DoesNotSendAgain() {
        var sent = new List<string>();
        var arrival = new ZoneArrival(() => sent.Add(Reconcile));

        arrival.PlayerAdded();
        arrival.ClientZoned();
        arrival.FallbackElapsed();

        Assert.Equal([Reconcile], sent);
    }

    [Fact]
    public void ClientZonedWithoutAnAddedPlayer_SendsNothing() {
        var sent = new List<string>();
        var arrival = new ZoneArrival(() => sent.Add(Reconcile));

        arrival.ClientZoned();

        Assert.Empty(sent);
    }
}
