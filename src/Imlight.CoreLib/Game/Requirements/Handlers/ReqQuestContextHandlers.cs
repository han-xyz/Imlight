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
 * REQUIREMENT HANDLERS
 * ========================================================================
 * 
 * PURPOSE:
 * Evaluates ReqIsQuest and ReqIsGoal against the quest and goal the check runs for.
 * 
 * USAGE EXAMPLE:
 * Registered by RequirementDispatcher; a quest or goal check passes its names in the context.
 * 
 * NOTE:
 * A check that is not about a quest has no quest name, so both fail there.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqIsQuest: the check runs for the named quest.
/// </summary>
internal sealed class ReqIsQuestHandler : BaseRequirementHandler<ReqIsQuest> {

    public override bool Evaluate(IRequirementContext context)
        => !Requirement.m_questName.IsEmpty
        && string.Equals(context.GetQuestName(), Requirement.m_questName, StringComparison.Ordinal);

}

/// <summary>
/// ReqIsGoal: the check runs for the named goal of the named quest.
/// </summary>
internal sealed class ReqIsGoalHandler : BaseRequirementHandler<ReqIsGoal> {

    public override bool Evaluate(IRequirementContext context)
        => !Requirement.m_goalName.IsEmpty
        && string.Equals(context.GetQuestName(), Requirement.m_questName, StringComparison.Ordinal)
        && string.Equals(context.GetGoalName(), Requirement.m_goalName, StringComparison.Ordinal);

}
