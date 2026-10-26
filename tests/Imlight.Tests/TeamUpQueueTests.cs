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

using System;
using System.Linq;
using Imlight.CoreLib.Game.World;
using Xunit;

namespace Imlight.Tests;

public sealed class TeamUpQueueTests {

    private static readonly TeamUpDungeon Triton = new("WizardCity/WC_Streets/Interiors/WC_Triton_Tower1", "0,0,0,0", "Triton Tower");
    private static readonly TeamUpDungeon Golem = new("WizardCity/WC_Streets/Interiors/WC_Golem_Tower1", "0,0,0,0", "Golem Tower");

    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    private TeamUpQueue NewQueue()
        => new(partySize: () => 4, maximumWait: () => TimeSpan.FromSeconds(120), clock: () => _now);

    [Fact]
    public void TakeReadyTeams_FullParty_FormsTeamOfTheFirstFour() {
        var queue = NewQueue();
        var sigilId = queue.Enqueue(1, Triton);
        foreach (var charId in new ulong[] { 2, 3, 4, 5 }) {
            queue.Enqueue(charId, Triton);
        }

        var team = Assert.Single(queue.TakeReadyTeams());

        Assert.Equal([1UL, 2, 3, 4], team.Members);
        Assert.Equal(Triton, team.Dungeon);
        Assert.Equal([5UL], queue.MembersOf(sigilId));
    }

    [Fact]
    public void TakeReadyTeams_TwoWaitingPastTheLimit_FormTeam() {
        var queue = NewQueue();
        queue.Enqueue(1, Triton);
        _now += TimeSpan.FromSeconds(60);
        queue.Enqueue(2, Triton);
        _now += TimeSpan.FromSeconds(61);

        var team = Assert.Single(queue.TakeReadyTeams());

        Assert.Equal([1UL, 2], team.Members);
    }

    [Fact]
    public void TakeReadyTeams_TwoWaitingWithinTheLimit_StayQueued() {
        var queue = NewQueue();
        queue.Enqueue(1, Triton);
        queue.Enqueue(2, Triton);
        _now += TimeSpan.FromSeconds(119);

        Assert.Empty(queue.TakeReadyTeams());
    }

    [Fact]
    public void TakeReadyTeams_OneWaitingPastTheLimit_StaysQueued() {
        var queue = NewQueue();
        var sigilId = queue.Enqueue(1, Triton);
        _now += TimeSpan.FromMinutes(10);

        Assert.Empty(queue.TakeReadyTeams());
        Assert.Equal([1UL], queue.MembersOf(sigilId));
    }

    [Fact]
    public void Enqueue_SecondDungeon_MovesThePlayer() {
        var queue = NewQueue();
        var triton = queue.Enqueue(1, Triton);
        var golem = queue.Enqueue(1, Golem);

        Assert.NotEqual(triton, golem);
        Assert.Empty(queue.MembersOf(triton));
        Assert.Equal([1UL], queue.MembersOf(golem));
    }

    [Fact]
    public void TakeReadyTeams_QueuesPerDungeon_DoNotMix() {
        var queue = NewQueue();
        queue.Enqueue(1, Triton);
        queue.Enqueue(2, Triton);
        queue.Enqueue(3, Golem);
        queue.Enqueue(4, Golem);

        Assert.Empty(queue.TakeReadyTeams());
        Assert.Equal(2, queue.OpenDungeons().Count);
    }

    [Fact]
    public void Remove_LastPlayer_ClosesTheDungeonQueue() {
        var queue = NewQueue();
        var sigilId = queue.Enqueue(1, Triton);

        Assert.True(queue.Remove(1));

        Assert.Null(queue.FindDungeon(sigilId));
        Assert.Empty(queue.OpenDungeons());
    }

}
