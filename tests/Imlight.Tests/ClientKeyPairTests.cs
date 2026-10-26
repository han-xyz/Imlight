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
 * LOGIN SESSION KEY EXPIRY TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for refusing a stored login session key past its lifetime.
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
using Imlight.CoreLib.WizardData.Collections;
using Xunit;

namespace Imlight.Tests;

public class ClientKeyPairTests {

    private static readonly TimeSpan s_lifetime = TimeSpan.FromHours(30);
    private static readonly DateTime s_issuedAt = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WithinLifetime_NotExpired() {
        var pair = new ClientKeyPair(1, 2, "key") { IssuedAt = s_issuedAt };

        Assert.False(pair.IsExpired(s_issuedAt + s_lifetime - TimeSpan.FromSeconds(1), s_lifetime));
    }

    [Fact]
    public void PastLifetime_Expired() {
        var pair = new ClientKeyPair(1, 2, "key") { IssuedAt = s_issuedAt };

        Assert.True(pair.IsExpired(s_issuedAt + s_lifetime, s_lifetime));
    }

    [Fact]
    public void NoStoredIssueTime_Expired() {
        var pair = new ClientKeyPair(1, 2, "key");

        Assert.True(pair.IsExpired(s_issuedAt, s_lifetime));
    }

}
