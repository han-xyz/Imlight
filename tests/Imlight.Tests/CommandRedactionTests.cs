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
 * COMMAND LOG REDACTION TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for keeping passwords out of the logged command text.
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

using Imlight.CoreLib.Game.Commands;
using Xunit;

namespace Imlight.Tests;

public class CommandRedactionTests {

    [Theory]
    [InlineData("account create bob hunter2", "account create [redacted]")]
    [InlineData("account password bob hunter2 hunter2", "account password [redacted]")]
    [InlineData("  Account PASSWORD  bob hunter2 hunter2", "Account PASSWORD [redacted]")]
    public void PasswordCommand_ArgumentsRedacted(string commandText, string expected) {
        Assert.Equal(expected, CommandDispatcher.RedactPasswords(commandText));
    }

    [Theory]
    [InlineData("account info bob")]
    [InlineData("account lock bob")]
    [InlineData("unknowngroup create bob hunter2")]
    public void OtherCommand_Unchanged(string commandText) {
        Assert.Equal(commandText, CommandDispatcher.RedactPasswords(commandText));
    }

}
