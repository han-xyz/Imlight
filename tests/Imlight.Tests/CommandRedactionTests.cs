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
 * COMMAND REDACTION TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Checks that password parameters are replaced before a command is logged.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * Created by: BobFromIHT with Claude
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imlight.CoreLib.Game.Commands.Protocols;
using Xunit;

namespace Imlight.Tests;

public class CommandRedactionTests {

    [Fact]
    public void AccountCreate_RedactsPasswordOnly() {
        Assert.True(new CommandAccountProtocol().TryRedactParameters("create", ["erin", "hunter2"], out var redacted));
        Assert.Equal(["erin", "<redacted>"], redacted);
    }

    [Fact]
    public void AccountPassword_RedactsBothCopies() {
        Assert.True(new CommandAccountProtocol().TryRedactParameters("PASSWORD", ["erin", "a", "a"], out var redacted));
        Assert.Equal(["erin", "<redacted>", "<redacted>"], redacted);
    }

    [Fact]
    public void CommandWithoutSensitiveParameters_Unchanged() {
        Assert.True(new CommandAccountProtocol().TryRedactParameters("lock", ["erin"], out var redacted));
        Assert.Equal(["erin"], redacted);
    }

    [Fact]
    public void UnknownCommand_NotHandled()
        => Assert.False(new CommandAccountProtocol().TryRedactParameters("nonsense", ["x"], out _));

}
