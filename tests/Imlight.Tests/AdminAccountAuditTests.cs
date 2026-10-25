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
 * ADMIN ACCOUNT AUDIT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the boot-time check of stored Administrator password hashes.
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

using Imlight.CoreLib.WizardData;
using Xunit;

namespace Imlight.Tests;

public class AdminAccountAuditTests {

    private static readonly string s_defaultHash = DatabaseUtilities.CreateHashedPassword("compsci");
    private static readonly string s_historicDefaultHash = DatabaseUtilities.CreateHashedPassword("dev9999");
    private static readonly string s_otherHash = DatabaseUtilities.CreateHashedPassword("s0me-l0ng-pass");

    [Fact]
    public void DefaultHash_ConfiguredUser_Reset() {
        var actions = AdminAccountAudit.Audit([new AdminAccountCredential("admin", s_defaultHash)], "admin");

        Assert.Equal([new AdminAccountAuditAction("admin", AdminAccountAuditActionKind.ResetToConfigured)], actions);
    }

    [Fact]
    public void DefaultHash_OtherAdmin_Refuse() {
        var actions = AdminAccountAudit.Audit([new AdminAccountCredential("admin", s_defaultHash)], "operator");

        Assert.Equal([new AdminAccountAuditAction("admin", AdminAccountAuditActionKind.RefuseStart)], actions);
    }

    [Fact]
    public void DefaultHash_ConfiguredUserDifferentCase_Reset() {
        var actions = AdminAccountAudit.Audit([new AdminAccountCredential("Admin", s_defaultHash)], "admin");

        Assert.Equal([new AdminAccountAuditAction("Admin", AdminAccountAuditActionKind.ResetToConfigured)], actions);
    }

    [Fact]
    public void HistoricDefaultHash_ConfiguredUser_Reset() {
        var actions = AdminAccountAudit.Audit([new AdminAccountCredential("dev1", s_historicDefaultHash)], "dev1");

        Assert.Equal([new AdminAccountAuditAction("dev1", AdminAccountAuditActionKind.ResetToConfigured)], actions);
    }

    [Fact]
    public void HistoricDefaultHash_OtherAdmin_Refuse() {
        var actions = AdminAccountAudit.Audit([new AdminAccountCredential("dev1", s_historicDefaultHash)], "admin");

        Assert.Equal([new AdminAccountAuditAction("dev1", AdminAccountAuditActionKind.RefuseStart)], actions);
    }

    [Fact]
    public void NoDefaultHash_NoAction() {
        var actions = AdminAccountAudit.Audit(
            [new AdminAccountCredential("admin", s_otherHash), new AdminAccountCredential("operator", s_otherHash)],
            "admin");

        Assert.Empty(actions);
    }

    [Fact]
    public void Mixed_ReturnsBoth() {
        var actions = AdminAccountAudit.Audit(
            [
                new AdminAccountCredential("admin", s_defaultHash),
                new AdminAccountCredential("operator", s_defaultHash),
                new AdminAccountCredential("moderator", s_otherHash),
            ],
            "operator");

        Assert.Equal(
            [
                new AdminAccountAuditAction("admin", AdminAccountAuditActionKind.RefuseStart),
                new AdminAccountAuditAction("operator", AdminAccountAuditActionKind.ResetToConfigured),
            ],
            actions);
    }

}
