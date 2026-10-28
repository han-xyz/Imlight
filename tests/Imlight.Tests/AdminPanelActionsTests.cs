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
 * ADMIN PANEL ACTIONS TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Checks the admin panel's action catalog: what each staff level is offered, how arguments are read, how the
 * own-character tools build their chat command, and that an action needing a live character refuses an offline one.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Akka.Actor;
using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public class AdminPanelActionsTests {

    [Fact]
    public void Catalog_IdsAreUnique() {
        var ids = PanelActions.All.Select(action => action.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Catalog_EachActionHasTheHandlerItsReachNeeds() {
        foreach (var action in PanelActions.All) {
            switch (action.Reach) {
                case PanelReach.Character or PanelReach.CharacterOnline:
                    Assert.NotNull(action.OnCharacter);
                    break;
                case PanelReach.Account or PanelReach.Server:
                    Assert.NotNull(action.OnAccount);
                    break;
                case PanelReach.Self:
                    Assert.NotNull(action.Command);
                    break;
            }

            Assert.True(action.Arguments.Length <= PanelActions.MaxArguments, action.Id);
        }
    }

    [Fact]
    public void Catalog_ToolCommandsUseEveryArgument() {
        foreach (var action in PanelActions.All.Where(action => action.Reach == PanelReach.Self)) {
            var placeholders = Regex.Matches(action.Command, @"\{(\d)\}").Select(match => match.Groups[1].Value).Distinct();

            Assert.Equal(action.Arguments.Length, placeholders.Count());
        }
    }

    [Theory]
    [InlineData(AuthLevel.QualityAssurance, "levelup", true)]
    [InlineData(AuthLevel.QualityAssurance, "ban", false)]
    [InlineData(AuthLevel.HallMonitor, "ban", true)]
    [InlineData(AuthLevel.HallMonitor, "statue", false)]
    [InlineData(AuthLevel.Administrator, "statue", true)]
    public void Describe_OffersOnlyWhatTheLevelAllows(AuthLevel level, string actionId, bool offered) {
        var actor = new Account { AuthLevel = level };

        var offeredIds = PanelActions.Describe(actor).Select(fields => fields[2]);

        Assert.Equal(offered, offeredIds.Contains(actionId));
    }

    [Fact]
    public void Describe_MarksOptionalArguments() {
        var questInfo = PanelActions.Describe(new Account { AuthLevel = AuthLevel.Administrator })
            .Single(fields => fields[2] == "questinfo");

        Assert.Equal(["action", "quests", "questinfo", "Quest Info", "online", "Quest name (optional)"], questInfo);
    }

    [Fact]
    public void ReadArguments_PadsAndTrims() {
        var action = PanelActions.Find("addreagents");

        Assert.True(PanelActions.TryReadArguments(action, [" 123 ", "4"], out var arguments, out _));
        Assert.Equal(["123", "4", ""], arguments);
    }

    [Fact]
    public void ReadArguments_RefusesAMissingRequiredArgument() {
        var action = PanelActions.Find("addreagents");

        Assert.False(PanelActions.TryReadArguments(action, ["123"], out _, out var refusal));
        Assert.Equal("Fill in Count.", refusal.Message);
    }

    [Fact]
    public void ReadArguments_AllowsAnEmptyOptionalArgument() {
        var action = PanelActions.Find("ratingset");

        Assert.True(PanelActions.TryReadArguments(action, ["1600"], out var arguments, out _));
        Assert.Equal("", arguments[1]);
    }

    [Theory]
    [InlineData("teleport", "WizardCity/WC_Ravenwood", "", "teleport WizardCity/WC_Ravenwood")]
    [InlineData("groupkick", "Ethan Dragonblade", "", "group kick Ethan Dragonblade")]
    [InlineData("setobj", "Open", "Gate 01", "debug setobj Open Gate 01")]
    [InlineData("gps", "", "", "debug gps")]
    public void BuildCommand_FillsTheChatCommand(string actionId, string first, string second, string expected) {
        var action = PanelActions.Find(actionId);

        Assert.Equal(expected, PanelActions.BuildCommand(action, [first, second, ""]));
    }

    [Fact]
    public void RunOnCharacter_RefusesALiveOnlyActionOnAnOfflineCharacter() {
        var wizard = new Wizard {
            CharId = 7,
            PlayerNameBehavior = new ServerWizPlayerNameBehavior { NameOverride = "Test Wizard" }
        };
        var action = PanelActions.Find("speed");

        var result = PanelActions.RunOnCharacter(action, CharacterTarget.Offline(wizard), ["50", "", ""],
            new Account { AuthLevel = AuthLevel.Administrator }, null);

        Assert.False(result.Succeeded);
        Assert.Equal("Test Wizard is offline, and Speed Bonus needs the character online.", result.Message);
    }

    [Fact]
    public void ReplyChannel_SendsTheStaffMemberTheServiceReply() {
        var system = ActorSystem.Create("admin-panel-actions-tests");
        try {
            var staff = Inbox.Create(system);
            new AdminReplyChannel(staff.Receiver, 42).Send("Quest granted.");

            var reply = Assert.IsType<ADMIN_112_PROTOCOL.MSG_ADMINACTIONREPLY>(staff.Receive(TimeSpan.FromSeconds(5)));
            Assert.Equal(42u, reply.RequestId);
            Assert.Equal("Quest granted.", reply.Message);
            Assert.False(reply.Final);
        }
        finally {
            system.Terminate().Wait(TimeSpan.FromSeconds(5));
        }
    }

}
