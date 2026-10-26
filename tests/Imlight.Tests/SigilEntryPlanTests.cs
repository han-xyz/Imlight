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

using System;
using System.Collections.Generic;
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
    public void ChooseOwner_LeaderOnThePad_LeaderOwns() {
        Assert.Equal(Leader, SigilEntryPlan.ChooseOwner(Member, Group(Leader, Member), padOccupants: [Leader, Member]));
    }

    [Fact]
    public void ChooseOwner_LeaderNotOnThePad_FirstEntrantOwns() {
        Assert.Equal(Member, SigilEntryPlan.ChooseOwner(Member, Group(Leader, Member), padOccupants: [Member]));
    }

    [Fact]
    public void ChooseOwner_LeaderOfflineOnThePad_FirstEntrantOwns() {
        var group = new GroupSnapshot(10, 20, Leader, [Offline(Leader), Online(Member)], 0, 0);

        Assert.Equal(Member, SigilEntryPlan.ChooseOwner(Member, group, padOccupants: [Leader, Member]));
    }

    [Fact]
    public void CountdownRecipients_AreTheOtherOnlineMembers() {
        var group = new GroupSnapshot(10, 20, Leader, [Online(Leader), Online(Member), Offline(Other)], 0, 0);

        var recipients = SigilEntryPlan.CountdownRecipients(Leader, group);

        Assert.Equal([Member], recipients.Select(member => member.CharId));
    }

    internal static GroupSnapshot Group(params ulong[] charIds)
        => new(10, 20, charIds[0], [.. charIds.Select(Online)], 0, 0);

    internal static GroupMemberSnapshot Online(ulong charId)
        => new(charId, "", null, ActorRefs.Nobody, 0);

    internal static GroupMemberSnapshot Offline(ulong charId)
        => new(charId, "", null, null, 0);

}

public sealed class DungeonRunBookTests {

    private const ulong Leader = 1;
    private const ulong Member = 2;
    private const ulong Stranger = 3;
    private const string Floor1 = "Dungeon/Floor1";
    private const string Floor2 = "Dungeon/Floor2";
    private const string Street = "Street/Avenue";

    private readonly Dictionary<ulong, GroupSnapshot> _groups = [];
    private DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly DungeonRunBook _book;

    public DungeonRunBookTests() {
        var group = SigilEntryPlanTests.Group(Leader, Member);
        _groups[Leader] = group;
        _groups[Member] = group;
        _book = new DungeonRunBook(charId => _groups.GetValueOrDefault(charId), () => TimeSpan.FromMinutes(30), () => _now);
    }

    [Fact]
    public void Enter_WithoutGroup_EntrantOwnsAFreshInstanceAndNoRun() {
        Assert.Equal(new InstanceEntry(Stranger, ResetInstance: true), _book.Enter(Stranger, Floor1, Street));
        Assert.Equal(0ul, _book.RunOwnerOf(Stranger, Floor1));
    }

    [Fact]
    public void Enter_GroupOnThePad_SharesTheLeadersRunAndOnlyTheFirstResets() {
        _book.Wait(Leader, Floor1);
        _book.Wait(Member, Floor1);

        Assert.Equal(new InstanceEntry(Leader, ResetInstance: true), _book.Enter(Member, Floor1, Street));
        Assert.Equal(new InstanceEntry(Leader, ResetInstance: false), _book.Enter(Leader, Floor1, Street));
        Assert.Equal(Leader, _book.RunOwnerOf(Member, Floor1));
    }

    [Fact]
    public void Enter_LeaderWhoseCountdownWentStale_DoesNotOwnTheRun() {
        _book.Wait(Leader, Floor1);
        _now += TimeSpan.FromMinutes(2);

        Assert.Equal(new InstanceEntry(Member, ResetInstance: true), _book.Enter(Member, Floor1, Street));
    }

    [Fact]
    public void Enter_OwnerReturningFromTheStreet_JoinsTheLiveRunWithoutReset() {
        EnterTogether();
        _book.Arrived(Leader, Street, isInstance: false, fromRunZone: true);

        Assert.Equal(new InstanceEntry(Leader, ResetInstance: false), _book.Enter(Leader, Floor1, Street));
    }

    [Fact]
    public void Enter_LateMemberWhileTheOwnerIsOnFloor2_JoinsWithoutReset() {
        _book.Enter(Leader, Floor1, Street);
        _book.Arrived(Leader, Floor1, isInstance: true, fromRunZone: false);
        Assert.Equal(Leader, _book.RouteTransfer(Leader, Floor1, Floor2)?.OwnerCharId);
        _book.Arrived(Leader, Floor2, isInstance: true, fromRunZone: true);

        Assert.Equal(new InstanceEntry(Leader, ResetInstance: false), _book.Enter(Member, Floor1, Street));
    }

    [Fact]
    public void RouteTransfer_FromARunFloor_FollowsTheOwnerToTheNextFloor() {
        EnterTogether();

        Assert.Equal(Leader, _book.RouteTransfer(Member, Floor1, Floor2)?.OwnerCharId);
        _book.Arrived(Member, Floor2, isInstance: true, fromRunZone: true);

        Assert.Equal(Leader, _book.RunOwnerOf(Member, Floor2));
    }

    [Fact]
    public void RouteTransfer_ToAFloorLeftByAnEarlierRun_ResetsItForTheFirstArrivalOnly() {
        EnterTogether();
        _book.RouteTransfer(Leader, Floor1, Floor2);
        _book.Arrived(Leader, Floor2, isInstance: true, fromRunZone: true);
        _book.Arrived(Leader, Street, isInstance: false, fromRunZone: true);
        _book.Arrived(Member, Street, isInstance: false, fromRunZone: true);
        _now += TimeSpan.FromMinutes(31);
        EnterTogether();

        Assert.Equal(new InstanceEntry(Leader, ResetInstance: true), _book.RouteTransfer(Member, Floor1, Floor2));
        Assert.Equal(new InstanceEntry(Leader, ResetInstance: false), _book.RouteTransfer(Leader, Floor1, Floor2));
    }

    [Fact]
    public void RouteTransfer_AfterAStreetHop_DoesNotFollowAgain() {
        EnterTogether();
        Assert.Equal(Leader, _book.RouteTransfer(Member, Floor1, Street)?.OwnerCharId);
        _book.Arrived(Member, Street, isInstance: false, fromRunZone: true);

        Assert.Null(_book.RouteTransfer(Member, Street, Floor2));
        Assert.Equal(0ul, _book.RunOwnerOf(Member, Street));
    }

    [Fact]
    public void RunOwnerOf_MemberWhoLeftTheGroup_IsNotRouted() {
        EnterTogether();
        _groups.Remove(Member);

        Assert.Equal(0ul, _book.RunOwnerOf(Member, Floor1));
        Assert.Null(_book.RouteTransfer(Member, Floor1, Floor2));
    }

    [Fact]
    public void RunOwnerOf_NonMember_IsNotRouted() {
        EnterTogether();

        Assert.Equal(0ul, _book.RunOwnerOf(Stranger, Floor1));
    }

    [Fact]
    public void Enter_AfterTheRunWasEmptyForTheIdleTime_StartsAFreshRun() {
        EnterTogether();
        _book.Arrived(Leader, Street, isInstance: false, fromRunZone: true);
        _book.Arrived(Member, Street, isInstance: false, fromRunZone: true);
        _now += TimeSpan.FromMinutes(31);

        Assert.Equal(0ul, _book.RunOwnerOf(Member, Floor1));
        Assert.Equal(new InstanceEntry(Member, ResetInstance: true), _book.Enter(Member, Floor1, Street));
    }

    [Fact]
    public void RunOwnerOf_AfterTheMembersReconnectInside_KeepsTheRunAlive() {
        EnterTogether();
        SetOnline(false);
        Assert.Equal(Leader, _book.RunOwnerOf(Leader, Floor1));
        _now += TimeSpan.FromMinutes(10);
        SetOnline(true);
        Assert.Equal(Leader, _book.RunOwnerOf(Member, Floor1));
        _now += TimeSpan.FromMinutes(25);

        Assert.Equal(Leader, _book.RunOwnerOf(Member, Floor1));
    }

    [Fact]
    public void TryTakeReturn_AfterTheRunEnded_SendsTheMemberBackToThePadStreetOnce() {
        EnterTogether();
        _book.Arrived(Leader, Street, isInstance: false, fromRunZone: true);
        _book.Arrived(Member, Street, isInstance: false, fromRunZone: true);
        _now += TimeSpan.FromMinutes(31);

        Assert.True(_book.TryTakeReturn(Member, Floor1, out var street));
        Assert.Equal(Street, street);
        Assert.False(_book.TryTakeReturn(Member, Floor1, out _));
    }

    [Fact]
    public void TryTakeReturn_WhileTheRunIsLive_KeepsTheMemberInside() {
        EnterTogether();

        Assert.False(_book.TryTakeReturn(Member, Floor1, out _));
    }

    private void SetOnline(bool isOnline) {
        GroupMemberSnapshot Snapshot(ulong charId) => isOnline ? SigilEntryPlanTests.Online(charId) : SigilEntryPlanTests.Offline(charId);
        var group = new GroupSnapshot(10, 20, Leader, [Snapshot(Leader), Snapshot(Member)], 0, 0);
        _groups[Leader] = group;
        _groups[Member] = group;
    }

    private void EnterTogether() {
        _book.Wait(Leader, Floor1);
        _book.Enter(Leader, Floor1, Street);
        _book.Enter(Member, Floor1, Street);
        _book.Arrived(Leader, Floor1, isInstance: true, fromRunZone: false);
        _book.Arrived(Member, Floor1, isInstance: true, fromRunZone: false);
    }

}

public sealed class ZoneInstanceOwnersRunRouteTests {

    [Fact]
    public void Resolve_PrivateAssignment_IsNotReplacedByARunRoute() {
        const string Arena = "Tests/RunRouteArena";
        ZoneInstanceOwners.Assign(7, Arena, 70);

        var resolved = ZoneInstanceOwners.Resolve(7, Arena, houseOwnerCharId: 0, runOwnerOf: (_, _) => 80);
        ZoneInstanceOwners.Clear(7, Arena);

        Assert.Equal((70ul, true), resolved);
    }

    [Fact]
    public void Resolve_RunRoute_SendsTheAttachToTheRunOwnersPrivateCopy() {
        Assert.Equal((80ul, true), ZoneInstanceOwners.Resolve(7, "Tests/RunFloor", houseOwnerCharId: 0, runOwnerOf: (_, _) => 80));
    }

}
