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
 * AUTHORITY REQUESTER
 * ========================================================================
 *
 * PURPOSE:
 * Provides a centralized authority request system for checking account authority levels
 * before performing privileged actions.
 *
 * USAGE EXAMPLE:
 * bool isAuthorized = AuthorityRequester.RequestAuthority(AuthLevel.Admin, 
 *                                                         currentAccount, 
 *                                                         "Accessing system settings");
 *
 * NOTE:
 * Automatically logs unauthorized access attempts as suspicious behavior.
 * Any suspicious behavior is recorded to the account as an infraction.
 *
 * TODO:
 * - 
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Date: 3/19/2025
 */

using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Auth;

/// <summary>
/// Manages account authorization level checks and logging for privileged actions.
/// </summary>
/// <remarks>
/// Automatically records and logs unauthorized access attempts as suspicious behavior.
/// </remarks>
internal static class AuthorityRequester {

    /// <summary>
    /// Checks if the account has the required authority level to perform the action.
    /// </summary>
    /// <param name="target">The required authority level.</param>
    /// <param name="account">The account to check.</param>
    /// <param name="reason">The reason for the authority request.</param>
    /// <param name="requireAdminMode">Whether staff must have admin mode on. Only commands that switch admin mode
    /// itself pass false.</param>
    /// <returns>True if the account has the required authority level, false otherwise.</returns>
    /// <remarks>
    /// Automatically records and logs unauthorized access attempts as suspicious behavior. Staff who have not
    /// enabled admin mode are refused without an infraction.
    /// </remarks>
    internal static bool RequestAuthority(AuthLevel target, Account account, string reason = null, bool requireAdminMode = true) {
        if (requireAdminMode && account.AuthLevel >= target && !account.AdminModeEnabled) {
            Logger.Information("Account {0} needs admin mode for {1}.",
                Logger.Args(account.Username, reason ?? "an authority request"));

            return false;
        }

        if (account.AuthLevel < target) {
            Logger.Warning("Account {0} attempted authority request without auth level {1} ({2}) "
                + "This is considered suspicious behavior and will be logged.",
                Logger.Args(account.Username, target, reason ?? "No reason given"));

            account.AddInfraction(InfractionType.SuspiciousBehavior, "Attempted to use commands without auth level");

            return false;
        }
        else {
            return true;
        }
    }

    /// <summary>
    /// Checks if the caller may act on the target account: the caller must outrank the target, except that an
    /// Administrator may act on another Administrator.
    /// </summary>
    /// <param name="target">The account being acted on.</param>
    /// <param name="caller">The account performing the action.</param>
    /// <param name="reason">The reason for the authority request.</param>
    /// <returns>True if the caller has authority over the target, false otherwise.</returns>
    /// <remarks>
    /// Automatically records and logs unauthorized access attempts as suspicious behavior.
    /// </remarks>
    internal static bool RequestAuthorityOver(Account target, Account caller, string reason = null) {
        if (HasAuthorityOver(caller.AuthLevel, target.AuthLevel)) {
            return true;
        }

        Logger.Warning("Account {Caller} attempted to act on account {Target} without authority over its auth level {TargetLevel} ({Reason}) "
            + "This is considered suspicious behavior and will be logged.",
            Logger.Args(caller.Username, target.Username, target.AuthLevel, reason ?? "No reason given"));

        caller.AddInfraction(InfractionType.SuspiciousBehavior, "Attempted to use commands without auth level");

        return false;
    }

    /// <summary>
    /// Checks if an account at the caller's auth level may act on an account at the target's auth level.
    /// </summary>
    /// <param name="caller">The auth level of the account performing the action.</param>
    /// <param name="target">The auth level of the account being acted on.</param>
    /// <returns>True if the caller outranks the target, or both are Administrator.</returns>
    internal static bool HasAuthorityOver(AuthLevel caller, AuthLevel target)
        => caller > target || (caller == AuthLevel.Administrator && target == AuthLevel.Administrator);

}
