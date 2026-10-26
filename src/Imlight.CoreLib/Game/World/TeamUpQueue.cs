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
 * TEAM UP
 * ========================================================================
 * 
 * PURPOSE:
 * The team-up queue: one queue per dungeon, each identified by the sigil id the client sees. A queue forms a team
 * once it holds a full party, or once its oldest player has waited the maximum wait with at least two queued.
 * 
 * USAGE EXAMPLE:
 * TeamUpService enqueues from MSG_REQUESTTEAMUP and MSG_REQUESTTEAMHELPJOIN and polls TakeReadyTeams.
 * 
 * NOTE:
 * A player is in at most one queue; queueing for another dungeon moves them.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.CoreLib.Game.Groups;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// A dungeon players can team up for: the sigil's destination and the name shown in the team-up window.
/// </summary>
internal sealed record TeamUpDungeon(string DungeonZone, string DestinationLoc, string SigilName);

/// <summary>
/// Players taken from a queue together, in the order they queued; the first leads the group.
/// </summary>
internal sealed record TeamUpTeam(uint SigilId, TeamUpDungeon Dungeon, IReadOnlyList<ulong> Members);

internal sealed class TeamUpQueue(Func<int> partySize, Func<TimeSpan> maximumWait, Func<DateTimeOffset> clock) {

    private const int MinimumTeamSize = 2;
    private const int DefaultPartySize = 4;
    private const int DefaultMaximumWaitSeconds = 120;

    internal static TeamUpQueue Shared { get; } = new(
        () => Math.Clamp(ConfigurationManager.GetValue("TeamUp.PartySize", DefaultPartySize), MinimumTeamSize, GroupDirectory.MaxGroupSize),
        () => TimeSpan.FromSeconds(ConfigurationManager.GetValue("TeamUp.MaximumWaitSeconds", DefaultMaximumWaitSeconds)),
        () => DateTimeOffset.UtcNow);

    private readonly object _lock = new();
    private readonly Dictionary<uint, DungeonQueue> _queues = [];
    private uint _lastSigilId;

    /// <summary>
    /// Queues the player for the dungeon, leaving any other queue, and returns the dungeon's sigil id.
    /// </summary>
    internal uint Enqueue(ulong charId, TeamUpDungeon dungeon) {
        lock (_lock) {
            var queue = _queues.Values.FirstOrDefault(open
                => string.Equals(open.Dungeon.DungeonZone, dungeon.DungeonZone, StringComparison.OrdinalIgnoreCase));
            if (queue?.Waiting.Any(waiting => waiting.CharId == charId) == true) {
                return queue.SigilId;
            }

            RemoveLocked(charId);
            if (queue is null) {
                queue = new DungeonQueue(++_lastSigilId, dungeon);
                _queues[queue.SigilId] = queue;
            }

            queue.Waiting.Add(new Waiting(charId, clock()));

            return queue.SigilId;
        }
    }

    /// <summary>
    /// Takes the player out of their queue; false when they were not queued.
    /// </summary>
    internal bool Remove(ulong charId) {
        lock (_lock) {
            return RemoveLocked(charId);
        }
    }

    internal IReadOnlyList<ulong> MembersOf(uint sigilId) {
        lock (_lock) {
            return _queues.TryGetValue(sigilId, out var queue) ? [.. queue.Waiting.Select(waiting => waiting.CharId)] : [];
        }
    }

    internal IReadOnlyList<ulong> QueueMatesOf(ulong charId) {
        lock (_lock) {
            var queue = _queues.Values.FirstOrDefault(open => open.Waiting.Any(waiting => waiting.CharId == charId));

            return queue is null ? [] : [.. queue.Waiting.Select(waiting => waiting.CharId).Where(mate => mate != charId)];
        }
    }

    internal TeamUpDungeon FindDungeon(uint sigilId) {
        lock (_lock) {
            return _queues.GetValueOrDefault(sigilId)?.Dungeon;
        }
    }

    internal IReadOnlyList<(uint SigilId, TeamUpDungeon Dungeon)> OpenDungeons() {
        lock (_lock) {
            return [.. _queues.Values.Select(queue => (queue.SigilId, queue.Dungeon))];
        }
    }

    /// <summary>
    /// Seconds until the queue's oldest player has waited the maximum wait; zero once it has.
    /// </summary>
    internal uint SecondsLeft(uint sigilId) {
        lock (_lock) {
            if (!_queues.TryGetValue(sigilId, out var queue) || queue.Waiting.Count == 0) {
                return 0;
            }

            var left = queue.Waiting[0].Since + maximumWait() - clock();

            return left > TimeSpan.Zero ? (uint) Math.Ceiling(left.TotalSeconds) : 0;
        }
    }

    /// <summary>
    /// Removes and returns every team ready to enter: full parties first, then a queue whose oldest player has
    /// waited the maximum wait, when it holds at least two.
    /// </summary>
    internal IReadOnlyList<TeamUpTeam> TakeReadyTeams() {
        lock (_lock) {
            var size = partySize();
            var now = clock();
            var teams = new List<TeamUpTeam>();

            foreach (var queue in _queues.Values.ToList()) {
                while (queue.Waiting.Count >= size) {
                    teams.Add(TakeTeam(queue, size));
                }

                if (queue.Waiting.Count >= MinimumTeamSize && now - queue.Waiting[0].Since >= maximumWait()) {
                    teams.Add(TakeTeam(queue, queue.Waiting.Count));
                }

                if (queue.Waiting.Count == 0) {
                    _queues.Remove(queue.SigilId);
                }
            }

            return teams;
        }
    }

    private static TeamUpTeam TakeTeam(DungeonQueue queue, int count) {
        var members = queue.Waiting.Take(count).Select(waiting => waiting.CharId).ToList();
        queue.Waiting.RemoveRange(0, count);

        return new TeamUpTeam(queue.SigilId, queue.Dungeon, members);
    }

    private bool RemoveLocked(ulong charId) {
        var queue = _queues.Values.FirstOrDefault(open => open.Waiting.Any(waiting => waiting.CharId == charId));
        if (queue is null) {
            return false;
        }

        queue.Waiting.RemoveAll(waiting => waiting.CharId == charId);
        if (queue.Waiting.Count == 0) {
            _queues.Remove(queue.SigilId);
        }

        return true;
    }

    private sealed record Waiting(ulong CharId, DateTimeOffset Since);

    private sealed record DungeonQueue(uint SigilId, TeamUpDungeon Dungeon) {

        public List<Waiting> Waiting { get; } = [];

    }

}
