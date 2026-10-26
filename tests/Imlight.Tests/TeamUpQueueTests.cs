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
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Game.World;
using Xunit;

namespace Imlight.Tests;

public sealed class TeamUpQueueTests {

    private static readonly TeamUpDungeon Triton = new("WizardCity/WC_Streets/Interiors/WC_Triton_Tower1", "0,0,0,0", "Triton Tower");
    private static readonly TeamUpDungeon Golem = new("WizardCity/WC_Streets/Interiors/WC_Golem_Tower1", "0,0,0,0", "Golem Tower");

    private static readonly Func<ulong, TeamUpSigil, bool> Everyone = (_, _) => true;

    private static TeamUpSigil SigilIn(string zone = "WizardCity/WC_Streets")
        => new(zone, 7, "0,0,0,0", 370f);

    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    private TeamUpQueue NewQueue()
        => new(partySize: () => 4, maximumWait: () => TimeSpan.FromSeconds(120), clock: () => _now);

    [Fact]
    public void TakeReadyTeams_FullParty_FormsTeamOfTheFirstFour() {
        var queue = NewQueue();
        var sigilId = queue.Enqueue(1, Triton, SigilIn());
        foreach (var charId in new ulong[] { 2, 3, 4, 5 }) {
            queue.Enqueue(charId, Triton, SigilIn());
        }

        var team = Assert.Single(queue.TakeReadyTeams(Everyone, out _));

        Assert.Equal([1UL, 2, 3, 4], team.Members);
        Assert.Equal(Triton, team.Dungeon);
        Assert.Equal([5UL], queue.MembersOf(sigilId));
    }

    [Fact]
    public void TakeReadyTeams_TwoWaitingPastTheLimit_FormTeam() {
        var queue = NewQueue();
        queue.Enqueue(1, Triton, SigilIn());
        _now += TimeSpan.FromSeconds(60);
        queue.Enqueue(2, Triton, SigilIn());
        _now += TimeSpan.FromSeconds(61);

        var team = Assert.Single(queue.TakeReadyTeams(Everyone, out _));

        Assert.Equal([1UL, 2], team.Members);
    }

    [Fact]
    public void TakeReadyTeams_TwoWaitingWithinTheLimit_StayQueued() {
        var queue = NewQueue();
        queue.Enqueue(1, Triton, SigilIn());
        queue.Enqueue(2, Triton, SigilIn());
        _now += TimeSpan.FromSeconds(119);

        Assert.Empty(queue.TakeReadyTeams(Everyone, out _));
    }

    [Fact]
    public void TakeReadyTeams_OneWaitingPastTheLimit_StaysQueued() {
        var queue = NewQueue();
        var sigilId = queue.Enqueue(1, Triton, SigilIn());
        _now += TimeSpan.FromMinutes(10);

        Assert.Empty(queue.TakeReadyTeams(Everyone, out _));
        Assert.Equal([1UL], queue.MembersOf(sigilId));
    }

    [Fact]
    public void Enqueue_SecondDungeon_MovesThePlayer() {
        var queue = NewQueue();
        var triton = queue.Enqueue(1, Triton, SigilIn());
        var golem = queue.Enqueue(1, Golem, SigilIn());

        Assert.NotEqual(triton, golem);
        Assert.Empty(queue.MembersOf(triton));
        Assert.Equal([1UL], queue.MembersOf(golem));
    }

    [Fact]
    public void TakeReadyTeams_QueuesPerDungeon_DoNotMix() {
        var queue = NewQueue();
        queue.Enqueue(1, Triton, SigilIn());
        queue.Enqueue(2, Triton, SigilIn());
        queue.Enqueue(3, Golem, SigilIn());
        queue.Enqueue(4, Golem, SigilIn());

        Assert.Empty(queue.TakeReadyTeams(Everyone, out _));
        Assert.Equal(2, queue.OpenDungeons().Count);
    }

    [Fact]
    public void Remove_LastPlayer_ClosesTheDungeonQueue() {
        var queue = NewQueue();
        var sigilId = queue.Enqueue(1, Triton, SigilIn());

        Assert.True(queue.Remove(1));

        Assert.Null(queue.FindDungeon(sigilId));
        Assert.Empty(queue.OpenDungeons());
    }

    [Fact]
    public void SigilIdOf_AndQueueMatesOf_OnlyTheirOwnQueue() {
        var queue = NewQueue();
        var triton = queue.Enqueue(1, Triton, SigilIn());
        queue.Enqueue(2, Triton, SigilIn());
        queue.Enqueue(3, Golem, SigilIn());

        Assert.Equal(triton, queue.SigilIdOf(1));
        Assert.Equal(0u, queue.SigilIdOf(4));
        Assert.Equal([2UL], queue.QueueMatesOf(1));
        Assert.Empty(queue.QueueMatesOf(3));
    }

    [Fact]
    public void Enqueue_WithoutAConfirmedDungeonOrSigil_RefusesThePlayer() {
        var queue = NewQueue();

        Assert.Equal(0u, queue.Enqueue(1, null, SigilIn()));
        Assert.Equal(0u, queue.Enqueue(1, Triton, null));
        Assert.Empty(queue.OpenDungeons());
        Assert.Equal(0u, queue.SigilIdOf(1));
    }

    [Fact]
    public void FindSigil_ReturnsTheSigilTheFirstPlayerQueuedAt() {
        var queue = NewQueue();
        var sigil = SigilIn("WizardCity/WC_Ravenwood");
        var sigilId = queue.Enqueue(1, Triton, sigil);

        Assert.Equal(sigil, queue.FindSigil(sigilId));
        Assert.Null(queue.FindSigil(sigilId + 1));
    }

    [Fact]
    public void TakeReadyTeams_ChecksEachMemberAgainstTheSigilTheyQueuedAt() {
        var queue = NewQueue();
        var street = SigilIn();
        var ravenwood = SigilIn("WizardCity/WC_Ravenwood");
        queue.Enqueue(1, Triton, street);
        queue.Enqueue(2, Triton, ravenwood);
        var seen = new List<(ulong, string)>();

        queue.TakeReadyTeams((charId, sigil) => { seen.Add((charId, sigil.Zone)); return true; }, out _);

        Assert.Equal([(1UL, street.Zone), (2UL, ravenwood.Zone)], seen);
    }

    [Fact]
    public void TakeReadyTeams_DropsStaleMembers_AndFormsWhenTheMinimumHolds() {
        var queue = NewQueue();
        foreach (var charId in new ulong[] { 1, 2, 3, 4 }) {
            queue.Enqueue(charId, Triton, SigilIn());
        }

        _now += TimeSpan.FromSeconds(121);
        var teams = queue.TakeReadyTeams((charId, _) => charId != 3, out var dropped);

        Assert.Equal([1UL, 2, 4], Assert.Single(teams).Members);
        Assert.Equal([3UL], dropped.Select(drop => drop.CharId));
        Assert.Equal(0u, queue.SigilIdOf(3));
    }

    [Fact]
    public void TakeReadyTeams_DroppingBelowTheMinimum_FormsNothingAndKeepsTheRest() {
        var queue = NewQueue();
        var sigilId = queue.Enqueue(1, Triton, SigilIn());
        queue.Enqueue(2, Triton, SigilIn());
        _now += TimeSpan.FromSeconds(121);

        var teams = queue.TakeReadyTeams((charId, _) => charId == 1, out var dropped);

        Assert.Empty(teams);
        Assert.Equal([(2UL, sigilId)], dropped);
        Assert.Equal([1UL], queue.MembersOf(sigilId));
    }

}
