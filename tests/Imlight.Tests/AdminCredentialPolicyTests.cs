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
 * ADMIN CREDENTIAL POLICY TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the boot-time admin credential check.
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
 * Last Updated: 10/25/2026
 */

using Imlight.Common;
using Xunit;

namespace Imlight.Tests;

public class AdminCredentialPolicyTests {

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Rejected(string? password) {
        Assert.False(AdminCredentialPolicy.Validate("admin", password).IsAccepted);
    }

    [Theory]
    [InlineData("compsci")]
    [InlineData("COMPSCI")]
    public void Compsci_Rejected(string password) {
        Assert.False(AdminCredentialPolicy.Validate("admin", password).IsAccepted);
    }

    [Theory]
    [InlineData("dev9999")]
    [InlineData("2034")]
    public void Policy_HistoricDefault_Rejected(string password) {
        Assert.False(AdminCredentialPolicy.Validate("admin", password).IsAccepted);
    }

    [Fact]
    public void Other_Accepted() {
        Assert.True(AdminCredentialPolicy.Validate("admin", "s0me-l0ng-pass").IsAccepted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyUsername_Rejected(string? username) {
        Assert.False(AdminCredentialPolicy.Validate(username, "s0me-l0ng-pass").IsAccepted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("compsci")]
    public void Message_NamesIniKeyAndEnvOverride(string password) {
        var verdict = AdminCredentialPolicy.Validate("admin", password);

        Assert.Contains("Database.AdminAccountPassword", verdict.Message);
        Assert.Contains("IMLIGHT__Database__AdminAccountPassword", verdict.Message);
    }

}
