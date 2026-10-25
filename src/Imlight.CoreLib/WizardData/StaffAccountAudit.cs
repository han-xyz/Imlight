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
 * STAFF ACCOUNT AUDIT
 * ========================================================================
 * 
 * PURPOSE:
 * Finds stored staff accounts (any AuthLevel above None) that still carry a known default password hash
 * and decides, per account, whether boot resets it to the configured password or refuses to start.
 * 
 * USAGE EXAMPLE:
 * StaffAccountAudit.Audit(staffAccounts, "Database.AdminAccountUsername" value)
 * 
 * NOTE:
 * Usernames compare case-insensitively, as the player database matches them.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.WizardData;

public sealed record StaffAccountCredential(string Username, string PasswordHash, AuthLevel AuthLevel);

public enum StaffAccountAuditActionKind {

    ResetToConfigured,
    RefuseStart

}

public sealed record StaffAccountAuditAction(string Username, AuthLevel AuthLevel, StaffAccountAuditActionKind Kind);

public static class StaffAccountAudit {

    private static readonly HashSet<string> s_knownDefaultPasswordHashes =
        [.. AdminCredentialPolicy.KnownDefaultPasswords.Select(DatabaseUtilities.CreateHashedPassword)];

    public static IReadOnlyList<StaffAccountAuditAction> Audit(
        IEnumerable<StaffAccountCredential> staffAccounts, string configuredUsername) {
        return staffAccounts
            .Where(staff => s_knownDefaultPasswordHashes.Contains(staff.PasswordHash))
            .Select(staff => new StaffAccountAuditAction(
                staff.Username,
                staff.AuthLevel,
                string.Equals(staff.Username, configuredUsername, StringComparison.OrdinalIgnoreCase)
                    ? StaffAccountAuditActionKind.ResetToConfigured
                    : StaffAccountAuditActionKind.RefuseStart))
            .ToList();
    }

}
