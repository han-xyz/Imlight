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
 * LOGIN THROTTLE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for refusing logins from an address after repeated failures.
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

using System;
using Imlight.CoreLib.Auth;
using Xunit;

namespace Imlight.Tests;

public class LoginThrottleTests {

    private const int Limit = 3;
    private const string Address = "203.0.113.7";
    private static readonly TimeSpan s_window = TimeSpan.FromMinutes(5);
    private static readonly DateTimeOffset s_start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BelowLimit_NotThrottled() {
        var throttle = new LoginThrottle(Limit, s_window);

        Fail(throttle, Address, Limit - 1, s_start);

        Assert.False(throttle.IsThrottled(Address, s_start));
    }

    [Fact]
    public void AtLimit_Throttled() {
        var throttle = new LoginThrottle(Limit, s_window);

        Fail(throttle, Address, Limit, s_start);

        Assert.True(throttle.IsThrottled(Address, s_start));
    }

    [Fact]
    public void Throttle_OnlyAffectsFailingAddress() {
        var throttle = new LoginThrottle(Limit, s_window);

        Fail(throttle, Address, Limit, s_start);

        Assert.False(throttle.IsThrottled("198.51.100.4", s_start));
    }

    [Fact]
    public void Throttle_LastsOneWindowFromTheLimitingFailure() {
        var throttle = new LoginThrottle(Limit, s_window);
        Fail(throttle, Address, Limit - 1, s_start);
        var limitingFailure = s_start + TimeSpan.FromMinutes(4);
        throttle.RecordFailure(Address, limitingFailure);

        Assert.True(throttle.IsThrottled(Address, limitingFailure + s_window - TimeSpan.FromSeconds(1)));
        Assert.False(throttle.IsThrottled(Address, limitingFailure + s_window));
    }

    [Fact]
    public void FailuresOutsideWindow_DoNotCount() {
        var throttle = new LoginThrottle(Limit, s_window);

        Fail(throttle, Address, Limit - 1, s_start);
        var later = s_start + s_window;
        throttle.RecordFailure(Address, later);

        Assert.False(throttle.IsThrottled(Address, later));
    }

    [Fact]
    public void AfterLockout_CountStartsAgain() {
        var throttle = new LoginThrottle(Limit, s_window);
        Fail(throttle, Address, Limit, s_start);
        var afterLockout = s_start + s_window;

        throttle.RecordFailure(Address, afterLockout);

        Assert.False(throttle.IsThrottled(Address, afterLockout));
    }

    private static void Fail(LoginThrottle throttle, string address, int count, DateTimeOffset at) {
        for (var i = 0; i < count; i++) {
            throttle.RecordFailure(address, at);
        }
    }

}
