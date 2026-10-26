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
 * Evaluates the requirements on the other characters of the player's account.
 * 
 * USAGE EXAMPLE:
 * Registered by RequirementDispatcher for content gated on an account's progress.
 * 
 * NOTE:
 * The account's characters are those loaded with it; one without a quest log has completed nothing.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqHighestCharacterLevelOnAccount: the highest level of any character on the account, this one included.
/// </summary>
internal sealed class ReqHighestCharacterLevelOnAccountHandler : BaseRequirementHandler<ReqHighestCharacterLevelOnAccount> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard?.MagicSchoolBehavior is null) {
            return false;
        }

        var highestLevel = (wizard.Account?.Characters ?? [])
            .Append(wizard)
            .Max(character => character.MagicSchoolBehavior?.Level ?? 0);

        return NumericRequirement.Compare(Requirement, highestLevel);
    }

}

/// <summary>
/// ReqOtherCharacterQuestCompleted: another character on the account has completed the quest.
/// </summary>
internal sealed class ReqOtherCharacterQuestCompletedHandler : BaseRequirementHandler<ReqOtherCharacterQuestCompleted> {

    private const string QUEST_COMPLETED_ENTRY = "Complete";

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        string questName = Requirement.m_quest;
        if (wizard?.Account is null || string.IsNullOrEmpty(questName)) {
            return false;
        }

        return wizard.Account.Characters.Any(character =>
            character.CharId != wizard.CharId
            && character.QuestBehavior is not null
            && character.HasQuestRegistryValue(questName, QUEST_COMPLETED_ENTRY));
    }

}
