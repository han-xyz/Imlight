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
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Misc;
using Xunit;

namespace Imlight.Tests;

public sealed class SocialAnswerTests {

    [Fact]
    public void PartyMemberZones_ListsEachOnlineMemberWithTheirZone() {
        var group = new GroupSnapshot(GroupId: 7, ChannelId: 8, LeaderCharId: 1, Members: [
            new GroupMemberSnapshot(1, "WizardCity/WC_Hub", ActorRefs.Nobody, ActorRefs.Nobody, 0),
            new GroupMemberSnapshot(2, "WizardCity/WC_Hub", ActorRefs.Nobody, ActorRefs.Nobody, 1),
            new GroupMemberSnapshot(3, "WizardCity/WC_Hub", ActorRefs.Nobody, null, 2),
        ], QuestId: 0, GoalId: 0);
        var currentZones = new Dictionary<ulong, string> {
            [1] = "WizardCity/WC_Hub",
            [2] = "WizardCity/WC_Streets/WC_Triton",
        };

        Assert.Equal("1:WizardCity/WC_Hub;2:WizardCity/WC_Streets/WC_Triton",
            PartyMemberZones.Format(group, charId => currentZones.GetValueOrDefault(charId)));
    }

    [Fact]
    public void ZoneChatRecipients_SkipTheSenderAndPlayersWhoBlockedThem() {
        OnlinePlayer[] players = [
            new() { CharacterId = 1 },
            new() { CharacterId = 2 },
            new() { CharacterId = 3 },
        ];

        var recipients = ZoneChatRecipients.Select(players, senderCharId: 1, blockedBy: [3]);

        Assert.Equal([2UL], recipients.Select(player => player.CharacterId));
    }

}
