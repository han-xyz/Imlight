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
 * CHAT FILTER SETTING TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for [Chat] FilterEnabled: it turns open chat into filtered chat for accounts below HallMonitor.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/30/2026
 */

using System.IO;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class ChatFilterSettingTests {

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    public void FilterOff_PlayerKeepsOpenChat(string value) {
        WithChatSection($"FilterEnabled = {value}\n", () => {
            var account = new Account();
            var permissions = account.GetChatPermissions();

            Assert.Equal(ChatMode.Open, account.GetEffectiveChatMode());
            Assert.True(account.HasOpenChat());
            Assert.True(account.GetAccountFlags().HasFlag(AccountFlags.CanOpenChat));
            Assert.NotEqual(0u, permissions & (uint) ChatPermissions.OpenChatLegacy);
            Assert.NotEqual(0u, permissions & (uint) ChatPermissions.ChatEnabled);
        });
    }

    [Fact]
    public void FilterOn_PlayerGetsFilteredChat() {
        WithChatSection("FilterEnabled = true\n", () => {
            var account = new Account();
            var flags = account.GetAccountFlags();
            var permissions = account.GetChatPermissions();

            Assert.Equal(ChatMode.Open, account.ChatMode);
            Assert.Equal(ChatMode.Filtered, account.GetEffectiveChatMode());
            Assert.False(account.HasOpenChat());
            Assert.True(flags.HasFlag(AccountFlags.CanChat));
            Assert.False(flags.HasFlag(AccountFlags.CanOpenChat));
            Assert.Equal(0u, permissions & (uint) ChatPermissions.OpenChatLegacy);
            Assert.Equal(0u, permissions & (uint) AccountFlags.CanOpenChat);
            Assert.NotEqual(0u, permissions & (uint) ChatPermissions.ChatEnabled);
        });
    }

    [Theory]
    [InlineData(AuthLevel.HallMonitor)]
    [InlineData(AuthLevel.Administrator)]
    public void FilterOn_StaffKeepOpenChat(AuthLevel authLevel) {
        WithChatSection("FilterEnabled = true\n", () => {
            var account = new Account { AuthLevel = authLevel };

            Assert.True(account.HasOpenChat());
            Assert.True(account.GetAccountFlags().HasFlag(AccountFlags.CanOpenChat));
            Assert.NotEqual(0u, account.GetChatPermissions() & (uint) ChatPermissions.OpenChatLegacy);
        });
    }

    [Fact]
    public void FilterOn_ClosedChatStaysClosed() {
        WithChatSection("FilterEnabled = true\n", () => {
            var account = new Account { ChatMode = ChatMode.Closed };

            Assert.Equal(ChatMode.Closed, account.GetEffectiveChatMode());
            Assert.Equal((uint) ChatPermissions.None, account.GetChatPermissions());
        });
    }

    private static void WithChatSection(string body, System.Action assert) {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Chat]\n" + body);
            ConfigurationManager.Initialize(iniFilePath);
            assert();
        } finally {
            File.Delete(iniFilePath);
        }
    }

}
