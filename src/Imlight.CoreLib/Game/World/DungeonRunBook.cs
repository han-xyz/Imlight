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
 * DUNGEON RUN BOOK
 * ========================================================================
 * 
 * PURPOSE:
 * Keeps the dungeon runs of groups: whose instance container a run lives in, which wizards belong to it and which
 * instanced zones it has reached, so its members are routed into the owner's copies and nobody else is.
 * 
 * USAGE EXAMPLE:
 * var entry = DungeonRunBook.Shared.Enter(charId, zoneName);
 * var ownerCharId = DungeonRunBook.Shared.RunOwnerOf(charId, zoneName);
 * 
 * NOTE:
 * - A zone is reset only when it is new to the group's live run; a run is created only when the group has none.
 * - A run ends when nobody has been inside it for Zone.InstanceIdleMinutes, or when its owner leaves the group. A member
 *   who leaves the group leaves the run. The zone copies expire on their own idle timer.
 * - In memory only.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imlight.CoreLib.Game.Groups;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// Tracks the dungeon runs of groups.
/// </summary>
/// <param name="findGroup">Gets a wizard's current group, or null when they are not grouped.</param>
/// <param name="idleLimit">How long a run with nobody inside lives; zero or less keeps it.</param>
/// <param name="clock">The current time.</param>
internal sealed class DungeonRunBook(Func<ulong, GroupSnapshot> findGroup, Func<TimeSpan> idleLimit, Func<DateTimeOffset> clock) {

    /// <summary>
    /// The book the game server uses.
    /// </summary>
    internal static DungeonRunBook Shared { get; } = new(
        charId => GroupRegistry.TryGetGroup(charId, out var group) ? group : null,
        () => TimeSpan.FromMinutes(Zone.Core.Zone.InstanceIdleMinutes),
        () => DateTimeOffset.UtcNow);

    // Well past the 10 second countdown: a wait this old belongs to a session that never ended or cancelled it.
    private static readonly TimeSpan WaitingLifetime = TimeSpan.FromMinutes(1);

    private readonly Lock _lock = new();
    private readonly Dictionary<ulong, DungeonRun> _runsByOwner = [];
    private readonly Dictionary<string, Dictionary<ulong, DateTimeOffset>> _waitingByZone = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ulong, EndedRun> _endedRunsByMember = [];

    /// <summary>
    /// Records a wizard counting down on the pad that leads to <paramref name="zoneName"/>.
    /// </summary>
    internal void Wait(ulong charId, string zoneName) {
        lock (_lock) {
            if (!_waitingByZone.TryGetValue(zoneName, out var waiting)) {
                waiting = [];
                _waitingByZone[zoneName] = waiting;
            }

            waiting[charId] = clock();
        }
    }

    /// <summary>
    /// Removes a wizard whose countdown was cancelled.
    /// </summary>
    internal void StopWaiting(ulong charId, string zoneName) {
        lock (_lock) {
            RemoveWaiting(charId, zoneName);
        }
    }

    /// <summary>
    /// Decides the instance a wizard enters when their sigil countdown ends. A grouped wizard joins the group's live run,
    /// or creates one in the chosen owner's container; only a zone new to the run is reset.
    /// </summary>
    /// <param name="charId">The wizard whose countdown ended.</param>
    /// <param name="zoneName">The zone the sigil leads to.</param>
    /// <param name="padZoneName">The zone the pad stands in, where a member returns once the run has ended.</param>
    internal InstanceEntry Enter(ulong charId, string zoneName, string padZoneName) {
        lock (_lock) {
            var occupants = RemoveWaiting(charId, zoneName);
            var group = findGroup(charId);
            if (group is null) {
                return new InstanceEntry(charId, ResetInstance: true);
            }

            var run = LiveRuns().FirstOrDefault(r => r.GroupId == group.GroupId);
            if (run is null) {
                var ownerCharId = SigilEntryPlan.ChooseOwner(charId, group, occupants);
                run = new DungeonRun(ownerCharId, group.GroupId, padZoneName, clock());
                _runsByOwner[ownerCharId] = run;
            }

            run.Members.Add(charId);
            _endedRunsByMember.Remove(charId);

            return new InstanceEntry(run.OwnerCharId, ResetInstance: run.Zones.Add(zoneName));
        }
    }

    /// <summary>
    /// Routes a transfer that leaves <paramref name="sourceZoneName"/> into the run owner's container, when the wizard
    /// belongs to a live run that has reached that zone; otherwise null. The first transfer to a zone new to the run
    /// resets it, so a copy left by an earlier run of the same owner is not reused.
    /// </summary>
    internal InstanceEntry RouteTransfer(ulong charId, string sourceZoneName, string destinationZoneName) {
        lock (_lock) {
            var run = FindRunOf(charId);
            if (run is null || !run.Zones.Contains(sourceZoneName)) {
                return null;
            }

            var isFirstArrival = !run.Zones.Contains(destinationZoneName) && run.ClaimedZones.Add(destinationZoneName);

            return new InstanceEntry(run.OwnerCharId, ResetInstance: isFirstArrival);
        }
    }

    /// <summary>
    /// Gets the owner whose copy of <paramref name="zoneName"/> the wizard attaches to, or 0 when no live run of theirs
    /// has reached that zone.
    /// </summary>
    internal ulong RunOwnerOf(ulong charId, string zoneName) {
        lock (_lock) {
            var run = FindRunOf(charId);

            return run is not null && run.Zones.Contains(zoneName) ? run.OwnerCharId : 0;
        }
    }

    /// <summary>
    /// Records where a wizard's transfer landed. An instanced zone reached from one of the run's zones joins the run;
    /// a zone outside the run puts the wizard outside it.
    /// </summary>
    internal void Arrived(ulong charId, string zoneName, bool isInstance, bool fromRunZone) {
        lock (_lock) {
            if (FindRunOf(charId) is not { } run) {
                return;
            }

            run.ClaimedZones.Remove(zoneName);
            if (isInstance && fromRunZone) {
                run.Zones.Add(zoneName);
            }

            if (run.Zones.Contains(zoneName)) {
                run.Inside.Add(charId);
                run.EmptySince = null;

                return;
            }

            run.Inside.Remove(charId);
            if (run.Inside.Count == 0) {
                run.EmptySince ??= clock();
            }
        }
    }

    /// <summary>
    /// Gets the street a wizard is sent back to after attaching to a zone of a run that has ended for them, so a relog
    /// does not leave them alone in a fresh copy of the dungeon. The return is given once.
    /// </summary>
    internal bool TryTakeReturn(ulong charId, string zoneName, out string padZoneName) {
        lock (_lock) {
            padZoneName = null;
            if (FindRunOf(charId)?.Zones.Contains(zoneName) == true
                || !_endedRunsByMember.TryGetValue(charId, out var ended) || !ended.Zones.Contains(zoneName)) {
                return false;
            }

            _endedRunsByMember.Remove(charId);
            padZoneName = ended.PadZoneName;

            return true;
        }
    }

    private DungeonRun FindRunOf(ulong charId)
        => LiveRuns().FirstOrDefault(run => run.Members.Contains(charId));

    private List<DungeonRun> LiveRuns() {
        var now = clock();
        var limit = idleLimit();
        foreach (var run in _runsByOwner.Values.ToList()) {
            foreach (var member in run.Members.Where(member => findGroup(member)?.GroupId != run.GroupId).ToList()) {
                EndRunFor(member, run);
            }

            // A wizard who disconnects inside stays in Inside, so a relog counts them again; only online wizards keep
            // the run from idling.
            var isAnyoneInside = run.Inside.Any(member => findGroup(member)?.GetMember(member)?.IsOnline == true);
            run.EmptySince = isAnyoneInside ? null : run.EmptySince ?? now;

            var hasIdledOut = limit > TimeSpan.Zero && run.EmptySince + limit <= now;
            if (hasIdledOut || !run.Members.Contains(run.OwnerCharId)) {
                foreach (var member in run.Members.ToList()) {
                    EndRunFor(member, run);
                }

                _runsByOwner.Remove(run.OwnerCharId);
            }
        }

        return [.. _runsByOwner.Values];
    }

    private void EndRunFor(ulong charId, DungeonRun run) {
        run.Members.Remove(charId);
        run.Inside.Remove(charId);
        _endedRunsByMember[charId] = new EndedRun(new HashSet<string>(run.Zones, StringComparer.OrdinalIgnoreCase), run.PadZoneName);
    }

    private HashSet<ulong> RemoveWaiting(ulong charId, string zoneName) {
        if (!_waitingByZone.TryGetValue(zoneName, out var waiting)) {
            return [];
        }

        var staleBefore = clock() - WaitingLifetime;
        HashSet<ulong> occupants = [.. waiting.Where(wait => wait.Value > staleBefore).Select(wait => wait.Key)];
        foreach (var stale in waiting.Where(wait => wait.Value <= staleBefore).Select(wait => wait.Key).ToList()) {
            waiting.Remove(stale);
        }

        waiting.Remove(charId);
        if (waiting.Count == 0) {
            _waitingByZone.Remove(zoneName);
        }

        return occupants;
    }

    private sealed record EndedRun(IReadOnlySet<string> Zones, string PadZoneName);

    private sealed class DungeonRun(ulong ownerCharId, ulong groupId, string padZoneName, DateTimeOffset createdAt) {

        public ulong OwnerCharId { get; } = ownerCharId;
        public ulong GroupId { get; } = groupId;
        public string PadZoneName { get; } = padZoneName;
        public HashSet<ulong> Members { get; } = [ownerCharId];
        public HashSet<string> Zones { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ClaimedZones { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<ulong> Inside { get; } = [];
        public DateTimeOffset? EmptySince { get; set; } = createdAt;

    }

}
