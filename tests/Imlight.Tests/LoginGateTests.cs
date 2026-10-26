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
 * LOGIN GATE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the order of ban, account and credential checks at login.
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
using System.Runtime.CompilerServices;
using Imlight.CoreLib.Auth;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public class LoginGateTests {

    [Fact]
    public void BannedOrigin_RefusedBeforeAccountLookupAndPassword() {
        var (admission, account) = LoginGate.Admit(
            isOriginBanned: () => true,
            findAccount: () => throw new InvalidOperationException("account looked up"),
            isPastDeadline: () => false,
            isCredentialValid: _ => throw new InvalidOperationException("password checked"));

        Assert.Equal(LoginGate.Admission.OriginBanned, admission);
        Assert.Null(account);
    }

    [Fact]
    public void UnknownAccount_BadCredentials() {
        var (admission, _) = Admit(account: null, isCredentialValid: true);

        Assert.Equal(LoginGate.Admission.BadCredentials, admission);
    }

    [Fact]
    public void WrongPassword_BadCredentials() {
        var (admission, account) = Admit(MakeAccount(isBanned: false), isCredentialValid: false);

        Assert.Equal(LoginGate.Admission.BadCredentials, admission);
        Assert.Null(account);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BannedOrLockedAccount_WrongPassword_BadCredentials(bool isBanned, bool isLocked) {
        var (admission, _) = Admit(MakeAccount(isBanned, isLocked), isCredentialValid: false);

        Assert.Equal(LoginGate.Admission.BadCredentials, admission);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BannedOrLockedAccount_RightPassword_AccountBanned(bool isBanned, bool isLocked) {
        var (admission, _) = Admit(MakeAccount(isBanned, isLocked), isCredentialValid: true);

        Assert.Equal(LoginGate.Admission.AccountBanned, admission);
    }

    [Fact]
    public void RightPassword_Admitted() {
        var expected = MakeAccount(isBanned: false);

        var (admission, account) = Admit(expected, isCredentialValid: true);

        Assert.Equal(LoginGate.Admission.Admitted, admission);
        Assert.Same(expected, account);
    }

    [Fact]
    public void PastDeadline_PasswordNotChecked() {
        var (admission, _) = LoginGate.Admit(
            isOriginBanned: () => false,
            findAccount: () => MakeAccount(isBanned: false),
            isPastDeadline: () => true,
            isCredentialValid: _ => throw new InvalidOperationException("password checked"));

        Assert.Equal(LoginGate.Admission.PastDeadline, admission);
    }

    private static (LoginGate.Admission, Account) Admit(Account? account, bool isCredentialValid)
        => LoginGate.Admit(() => false, () => account, () => false, _ => isCredentialValid);

    private static Account MakeAccount(bool isBanned, bool isLocked = false) {
        var account = (Account) RuntimeHelpers.GetUninitializedObject(typeof(Account));
        account.IsLocked = isLocked;
        account.InfractionHistory = new InfractionHistory(0, isBanned
            ? [new Infraction { InfractionType = InfractionType.Ban, Expiration = DateTime.UtcNow.AddDays(1) }]
            : []);

        return account;
    }

}
