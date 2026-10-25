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
 * The shipped default password "compsci" is rejected, as is an empty one.
 *
 * TODO:
 * 
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;

namespace Imlight.Common;

public static class AdminCredentialPolicy {

    private const string DefaultPassword = "compsci";

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

        if (password.Equals(DefaultPassword, StringComparison.OrdinalIgnoreCase)) {
            return AdminCredentialVerdict.Rejected(
                "Admin account password is the shipped default; change Database.AdminAccountPassword in Imlight.ini "
                + "or set the IMLIGHT__Database__AdminAccountPassword environment variable.");
        }

        return AdminCredentialVerdict.Accepted;
    }

}

public sealed record AdminCredentialVerdict(bool IsAccepted, string Message) {

    public static AdminCredentialVerdict Accepted { get; } = new(true, "");

    public static AdminCredentialVerdict Rejected(string message) => new(false, message);

}
