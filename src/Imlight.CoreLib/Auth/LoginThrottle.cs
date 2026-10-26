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
 * LOGIN THROTTLE
 * ========================================================================
 * 
 * PURPOSE:
 * Counts failed logins per source address and refuses an address for one window once it reaches the limit.
 * Attempts still running count against the limit, so parallel connections cannot outrun it.
 * 
 * USAGE EXAMPLE:
 * if (!throttle.TryBeginAttempt(ip, now)) { refuse; } try { ... } finally { throttle.EndAttempt(ip, now, failed); }
 * 
 * NOTE:
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

namespace Imlight.CoreLib.Auth;

internal sealed class LoginThrottle(int failureLimit, TimeSpan window) {

    private readonly Lock _lock = new();
    private readonly Dictionary<string, AddressRecord> _records = new(StringComparer.Ordinal);

    internal bool TryBeginAttempt(string address, DateTimeOffset now) {
        lock (_lock) {
            RemoveIdleRecords(now);

            if (!_records.TryGetValue(address, out var record)) {
                record = new AddressRecord();
                _records[address] = record;
            }

            DropFailuresOutsideWindow(record, now);
            if (record.LockedUntil > now || record.Failures.Count + record.AttemptsInFlight >= failureLimit) {
                return false;
            }

            record.AttemptsInFlight++;
            return true;
        }
    }

    internal void EndAttempt(string address, DateTimeOffset now, bool failed) {
        lock (_lock) {
            var record = _records[address];
            record.AttemptsInFlight--;
            if (!failed) {
                return;
            }

            record.Failures.Enqueue(now);
            DropFailuresOutsideWindow(record, now);

            if (record.Failures.Count >= failureLimit) {
                record.LockedUntil = now + window;
                record.Failures.Clear();
            }
        }
    }

    private void DropFailuresOutsideWindow(AddressRecord record, DateTimeOffset now) {
        while (record.Failures.Count > 0 && now - record.Failures.Peek() >= window) {
            _ = record.Failures.Dequeue();
        }
    }

    private void RemoveIdleRecords(DateTimeOffset now) {
        var idleAddresses = _records
            .Where(entry => entry.Value.AttemptsInFlight == 0
                && entry.Value.LockedUntil <= now
                && (entry.Value.Failures.Count == 0 || now - entry.Value.Failures.Last() >= window))
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var address in idleAddresses) {
            _ = _records.Remove(address);
        }
    }

    private sealed class AddressRecord {

        public Queue<DateTimeOffset> Failures { get; } = new();
        public int AttemptsInFlight { get; set; }
        public DateTimeOffset LockedUntil { get; set; }

    }

}
