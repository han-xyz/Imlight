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

using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Game.World;
using Xunit;

namespace Imlight.Tests;

public sealed class SigilEntryPlanTests {

    private const ulong Leader = 1;
    private const ulong Member = 2;
    private const ulong Other = 3;

    [Fact]
    public void Decide_WithoutGroup_EntrantOwnsAFreshInstance() {
        var entry = SigilEntryPlan.Decide(Member, group: null, padOccupants: [Member], liveOwnerCharId: 0);

        Assert.Equal(new SigilEntry(Member, ResetInstance: true), entry);
    }

    [Fact]
    public void Decide_LeaderOnThePad_LeaderOwnsAndTheFirstEntrantResets() {
        var entry = SigilEntryPlan.Decide(Member, Group(Leader, Member), padOccupants: [Leader, Member], liveOwnerCharId: 0);

        Assert.Equal(new SigilEntry(Leader, ResetInstance: true), entry);
    }

    [Fact]
    public void Decide_LeaderNotOnThePad_FirstEntrantOwns() {
        var entry = SigilEntryPlan.Decide(Member, Group(Leader, Member), padOccupants: [Member], liveOwnerCharId: 0);

        Assert.Equal(new SigilEntry(Member, ResetInstance: true), entry);
    }

    [Fact]
    public void Decide_LateMember_JoinsTheLiveInstanceWithoutReset() {
        var entry = SigilEntryPlan.Decide(Member, Group(Leader, Member), padOccupants: [Member], liveOwnerCharId: Leader);

        Assert.Equal(new SigilEntry(Leader, ResetInstance: false), entry);
    }

    [Fact]
    public void Decide_LiveOwnerOutsideTheGroup_IsNotJoined() {
        var entry = SigilEntryPlan.Decide(Member, Group(Leader, Member), padOccupants: [Member], liveOwnerCharId: Other);

        Assert.Equal(new SigilEntry(Member, ResetInstance: true), entry);
    }

    [Fact]
    public void Decide_LiveOwnerOffline_IsNotJoined() {
        var group = new GroupSnapshot(10, 20, Leader,
            [new GroupMemberSnapshot(Leader, "", null, null, 0), Online(Member)], 0, 0);

        var entry = SigilEntryPlan.Decide(Member, group, padOccupants: [Member], liveOwnerCharId: Leader);

        Assert.Equal(new SigilEntry(Member, ResetInstance: true), entry);
    }

    [Fact]
    public void CountdownRecipients_AreTheOtherOnlineMembers() {
        var group = new GroupSnapshot(10, 20, Leader,
            [Online(Leader), Online(Member), new GroupMemberSnapshot(Other, "", null, null, 0)], 0, 0);

        var recipients = SigilEntryPlan.CountdownRecipients(Leader, group);

        Assert.Equal([Member], recipients.Select(member => member.CharId));
    }

    private static GroupSnapshot Group(params ulong[] charIds)
        => new(10, 20, charIds[0], [.. charIds.Select(Online)], 0, 0);

    private static GroupMemberSnapshot Online(ulong charId)
        => new(charId, "", null, ActorRefs.Nobody, 0);

}
