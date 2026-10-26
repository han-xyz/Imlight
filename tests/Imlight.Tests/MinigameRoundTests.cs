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
using Imlight.CoreLib.Game.Minigames;
using Xunit;

namespace Imlight.Tests;

public sealed class MinigameRoundTests {

    private static readonly TimeSpan s_minimum = TimeSpan.FromSeconds(20);
    private static readonly DateTimeOffset s_start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Open_WithinTheMinimumOfTheLastOpen_IsRefused() {
        var round = new MinigameRound();
        Assert.True(round.TryOpen(s_start, s_minimum));

        Assert.False(round.TryOpen(s_start + TimeSpan.FromSeconds(5), s_minimum));
    }

    [Fact]
    public void Open_CalledTwiceAtOnce_SucceedsOnce() {
        var round = new MinigameRound();
        var opened = 0;

        System.Threading.Tasks.Parallel.For(0, 2, _ => {
            if (round.TryOpen(s_start, s_minimum)) {
                System.Threading.Interlocked.Increment(ref opened);
            }
        });

        Assert.Equal(1, opened);
    }

    [Fact]
    public void Open_AfterSettling_NeedsTheMinimumDuration() {
        var round = new MinigameRound();
        round.TryOpen(s_start, s_minimum);
        Assert.True(round.TrySettle());

        Assert.False(round.TryOpen(s_start + TimeSpan.FromSeconds(19), s_minimum));
        Assert.True(round.TryOpen(s_start + TimeSpan.FromSeconds(20), s_minimum));
    }

    [Fact]
    public void Settle_HappensOncePerOpenedRound() {
        var round = new MinigameRound();

        Assert.False(round.TrySettle());
        round.TryOpen(s_start, s_minimum);
        Assert.True(round.TrySettle());
        Assert.False(round.TrySettle());
    }

}
