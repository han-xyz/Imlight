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
 * ADMIN ACCOUNT AUDIT
 * ========================================================================
 * 
 * PURPOSE:
 * Finds stored Administrator accounts that still carry a known default password hash
 * and decides, per account, whether boot resets it to the configured password or refuses to start.
 * 
 * USAGE EXAMPLE:
 * AdminAccountAudit.Audit(administrators, "Database.AdminAccountUsername" value)
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

namespace Imlight.CoreLib.WizardData;

public sealed record AdminAccountCredential(string Username, string PasswordHash);

public enum AdminAccountAuditActionKind {

    ResetToConfigured,
    RefuseStart

}

public sealed record AdminAccountAuditAction(string Username, AdminAccountAuditActionKind Kind);

public static class AdminAccountAudit {

    // "compsci" is the ini default before L2; the rest were hard-coded for Administrator accounts in
    // src/Director before 106c24fd ("ref: remove embedded accounts") and are public in git history.
    private static readonly string[] s_knownDefaultPasswords = [
        "compsci", "debug", "dev9999", "2034", "9090", "2342", "2109", "joji1", "jefffakename",
        "jootysocoollike", "Niduus", "gay", "is that guy even a dev?", "isthatguyevenadev?",
    ];
    private static readonly HashSet<string> s_knownDefaultPasswordHashes =
        [.. s_knownDefaultPasswords.Select(DatabaseUtilities.CreateHashedPassword)];

    public static IReadOnlyList<AdminAccountAuditAction> Audit(
        IEnumerable<AdminAccountCredential> administrators, string configuredUsername) {
        return administrators
            .Where(admin => s_knownDefaultPasswordHashes.Contains(admin.PasswordHash))
            .Select(admin => new AdminAccountAuditAction(
                admin.Username,
                string.Equals(admin.Username, configuredUsername, StringComparison.OrdinalIgnoreCase)
                    ? AdminAccountAuditActionKind.ResetToConfigured
                    : AdminAccountAuditActionKind.RefuseStart))
            .ToList();
    }

}
