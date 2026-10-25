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
 * ADMIN CREDENTIAL POLICY
 * ========================================================================
 * 
 * PURPOSE:
 * Decides whether the configured admin account credentials are safe to boot with.
 * 
 * USAGE EXAMPLE:
 * var verdict = AdminCredentialPolicy.Validate(username, password);
 * if (!verdict.IsAccepted) { Logger.Fatal(verdict.Message); }
 * 
 * NOTE:
 * Known default passwords (the shipped "compsci" and those hard-coded in older builds) are rejected, as is an empty one.
 *
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.Common;

public static class AdminCredentialPolicy {

    /// <summary>
    /// Passwords boot rejects for the admin account: the old ini default "compsci", and the passwords hard-coded
    /// for staff accounts in src/Director before commit 106c24fd, which are public in git history.
    /// </summary>
    public static IReadOnlyList<string> KnownDefaultPasswords { get; } = [
        "compsci", "debug", "dev9999", "2034", "9090", "2342", "2109", "joji1", "jefffakename",
        "jootysocoollike", "Niduus", "gay", "is that guy even a dev?", "isthatguyevenadev?",
        "7969", "7878", "8080", "hm9999", "1121", "0091", "1041", "2290", "9142", "6142", "1224", "8723",
        "9871", "kcsd10", "4040", "7041", "1194", "0010", "4001", "9312",
    ];

    public static AdminCredentialVerdict Validate(string? username, string? password) {
        if (string.IsNullOrWhiteSpace(username)) {
            return AdminCredentialVerdict.Rejected(
                "Admin account username is empty; set Database.AdminAccountUsername in Imlight.ini "
                + "or the IMLIGHT__Database__AdminAccountUsername environment variable.");
        }

        if (string.IsNullOrWhiteSpace(password)) {
            return AdminCredentialVerdict.Rejected(
                "Admin account password is empty; set Database.AdminAccountPassword in Imlight.ini "
                + "or the IMLIGHT__Database__AdminAccountPassword environment variable.");
        }

        if (KnownDefaultPasswords.Contains(password, StringComparer.OrdinalIgnoreCase)) {
            return AdminCredentialVerdict.Rejected(
                "Admin account password is a known default password; change Database.AdminAccountPassword in Imlight.ini "
                + "or set the IMLIGHT__Database__AdminAccountPassword environment variable.");
        }

        return AdminCredentialVerdict.Accepted;
    }

}

public sealed record AdminCredentialVerdict(bool IsAccepted, string Message) {

    public static AdminCredentialVerdict Accepted { get; } = new(true, "");

    public static AdminCredentialVerdict Rejected(string message) => new(false, message);

}
