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
 * USER VALIDATION
 * ========================================================================
 *
 * PURPOSE:
 * Provides an validation mechnism forclients already logged in through
 * authentication and attempting to start the game.
 *
 * USAGE EXAMPLE:
 * ValidationDetails validationResult = UserValidator.Validate(sessionActor, validateMessage);
 * if (validationResult._result == UserValidateResult.Success) { ... }
 *
 * NOTE:
 * - On DEBUG builds, the session key is always valid. This is to allow
 *   developers to test the game without having to worry about session keys.
 *
 * TODO:
 * - 
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Date: 3/19/2025
 */

using System;
using Imlight.CoreLib.Shared.Cryptography;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imcodec.MessageLayer.Generated.LOGIN_7_PROTOCOL;

namespace Imlight.CoreLib.Auth;

internal enum UserValidateResult {

    Success = 0,
    AccountBanned = 87620544,
    MachineBanned = 1157331960,
    ValidateFailed = 246825817,
    Timeout = 1361855231,

}

/// <summary>
/// Manages user session validation for clients already logged in and starting the game.
/// </summary>
/// <remarks>
/// Differentiates from <see cref="UserAuthenticator"/> by handling post-login session validation.
/// </remarks>
internal static class UserValidator {

    internal class ValidationDetails {

        internal Account _account;
        internal string _sessionKey;
        internal UserValidateResult _result;

    }

    /// <summary>
    /// Validates the user. This happens after the game client has already authenticated,
    /// and is re-validating their session.
    /// </summary>
    /// <param name="sessionActor">The session actor.</param>
    /// <param name="validateMessage">The validation message.</param>
    /// <returns>The validation details.</returns>
    internal static ValidationDetails Validate(SessionActor sessionActor, MSG_USER_VALIDATE validateMessage) {
        var details = new ValidationDetails();

        string sessionKey = null;
        var (admission, matchedAccount) = LoginGate.Admit(
            () => InfractionCollection.IsMachineBanned(validateMessage.MachineID) || InfractionCollection.IsIpBanned(sessionActor.Ip),
            () => AccountCollection.GetAccountForCharacterList(validateMessage.UserID),
            () => false,
            account => {
                sessionKey = ClientKeyCollection.GetSessionKey(account.AccountId, validateMessage.MachineID);

                return !string.IsNullOrEmpty(sessionKey) && PassKey3.VerifyPK3(sessionKey, sessionActor.SessionID,
                    sessionActor.OfferTime, sessionActor.OfferMillisecondsIntoSecond, validateMessage.PassKey3);
            });

        details._result = admission switch {
            LoginGate.Admission.Admitted => UserValidateResult.Success,
            LoginGate.Admission.OriginBanned => UserValidateResult.MachineBanned,
            LoginGate.Admission.AccountBanned => UserValidateResult.AccountBanned,
            _ => UserValidateResult.ValidateFailed
        };
        if (admission != LoginGate.Admission.Admitted) {
            return details;
        }

        // If we've made it this far, the user is valid.
        matchedAccount.LastLoginMachineId = validateMessage.MachineID;
        matchedAccount.LastLoginTime = DateTime.UtcNow;
        matchedAccount.LastLoginIp = sessionActor.Ip;

        details._account = matchedAccount;
        details._sessionKey = sessionKey;
        details._result = UserValidateResult.Success;

        return details;
    }
    
}
