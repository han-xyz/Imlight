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
 * TELEPORT TRIGGER PICK TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for choosing one teleport trigger among several that pass.
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

using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Supervisors;
using Xunit;

namespace Imlight.Tests;

public sealed class TeleportTriggerPickTests {

    [Fact]
    public void Choose_OneHasGoalIncomplete_ItWinsOverLaterTrigger() {
        var lite = TriggerWith("Lite", GoalRequirement(GoalStatusRequirement.Incomplete));
        var full = TriggerWith("Full", GoalRequirement(GoalStatusRequirement.DontCare));

        var winner = ZoneTriggerSupervisor.ChooseTeleportTrigger([lite, full], new HashSet<string>(), out _);

        Assert.Same(lite, winner);
    }

    [Fact]
    public void Choose_NoneSpecific_LastWins() {
        var first = TriggerWith("First");
        var last = TriggerWith("Last", GoalRequirement(GoalStatusRequirement.DontCare));

        var winner = ZoneTriggerSupervisor.ChooseTeleportTrigger([first, last], new HashSet<string>(), out _);

        Assert.Same(last, winner);
    }

    [Fact]
    public void Choose_GoalNoLongerIncomplete_DontCareTriggerWins() {
        var full = TriggerWith("Full", GoalRequirement(GoalStatusRequirement.DontCare));
        var other = TriggerWith("Other");

        var winner = ZoneTriggerSupervisor.ChooseTeleportTrigger([other, full], new HashSet<string>(), out _);

        Assert.Same(full, winner);
    }

    private static Trigger TriggerWith(string name, params Requirement[] requirements)
        => new() { m_triggerName = name, m_requirements = new RequirementList { m_requirements = [.. requirements] } };

    private static ReqHasGoal GoalRequirement(GoalStatusRequirement status)
        => new() { m_questName = "Quest", m_goalName = "Goal 1", m_requiredStatus = status };

}
