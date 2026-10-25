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
 * STAFF ACCOUNT AUDIT TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the boot-time check of stored staff account password hashes.
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
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public class StaffAccountAuditTests {

    private static readonly string s_defaultHash = DatabaseUtilities.CreateHashedPassword("compsci");
    private static readonly string s_historicDefaultHash = DatabaseUtilities.CreateHashedPassword("dev9999");
    private static readonly string s_staffDefaultHash = DatabaseUtilities.CreateHashedPassword("7878");
    private static readonly string s_otherHash = DatabaseUtilities.CreateHashedPassword("s0me-l0ng-pass");

    [Fact]
    public void DefaultHash_ConfiguredUser_Reset() {
        var actions = StaffAccountAudit.Audit([new StaffAccountCredential("admin", s_defaultHash, AuthLevel.Administrator)], "admin");

        Assert.Equal([new StaffAccountAuditAction("admin", AuthLevel.Administrator, StaffAccountAuditActionKind.ResetToConfigured)], actions);
    }

    [Fact]
    public void DefaultHash_OtherAdmin_Refuse() {
        var actions = StaffAccountAudit.Audit([new StaffAccountCredential("admin", s_defaultHash, AuthLevel.Administrator)], "operator");

        Assert.Equal([new StaffAccountAuditAction("admin", AuthLevel.Administrator, StaffAccountAuditActionKind.RefuseStart)], actions);
    }

    [Fact]
    public void DefaultHash_ConfiguredUserDifferentCase_Reset() {
        var actions = StaffAccountAudit.Audit([new StaffAccountCredential("Admin", s_defaultHash, AuthLevel.Administrator)], "admin");

        Assert.Equal([new StaffAccountAuditAction("Admin", AuthLevel.Administrator, StaffAccountAuditActionKind.ResetToConfigured)], actions);
    }

    [Fact]
    public void HistoricDefaultHash_ConfiguredUser_Reset() {
        var actions = StaffAccountAudit.Audit([new StaffAccountCredential("dev1", s_historicDefaultHash, AuthLevel.Administrator)], "dev1");

        Assert.Equal([new StaffAccountAuditAction("dev1", AuthLevel.Administrator, StaffAccountAuditActionKind.ResetToConfigured)], actions);
    }

    [Fact]
    public void HistoricDefaultHash_OtherAdmin_Refuse() {
        var actions = StaffAccountAudit.Audit([new StaffAccountCredential("dev1", s_historicDefaultHash, AuthLevel.Administrator)], "admin");

        Assert.Equal([new StaffAccountAuditAction("dev1", AuthLevel.Administrator, StaffAccountAuditActionKind.RefuseStart)], actions);
    }

    [Fact]
    public void DefaultHash_StaffTier_Refuse() {
        var actions = StaffAccountAudit.Audit([new StaffAccountCredential("pk", s_staffDefaultHash, AuthLevel.HallMonitor)], "admin");

        Assert.Equal([new StaffAccountAuditAction("pk", AuthLevel.HallMonitor, StaffAccountAuditActionKind.RefuseStart)], actions);
    }

    [Fact]
    public void NoDefaultHash_NoAction() {
        var actions = StaffAccountAudit.Audit(
            [new StaffAccountCredential("admin", s_otherHash, AuthLevel.Administrator), new StaffAccountCredential("operator", s_otherHash, AuthLevel.Administrator)],
            "admin");

        Assert.Empty(actions);
    }

    [Fact]
    public void Mixed_ReturnsBoth() {
        var actions = StaffAccountAudit.Audit(
            [
                new StaffAccountCredential("admin", s_defaultHash, AuthLevel.Administrator),
                new StaffAccountCredential("operator", s_defaultHash, AuthLevel.Administrator),
                new StaffAccountCredential("moderator", s_otherHash, AuthLevel.Administrator),
            ],
            "operator");

        Assert.Equal(
            [
                new StaffAccountAuditAction("admin", AuthLevel.Administrator, StaffAccountAuditActionKind.RefuseStart),
                new StaffAccountAuditAction("operator", AuthLevel.Administrator, StaffAccountAuditActionKind.ResetToConfigured),
            ],
            actions);
    }

}
