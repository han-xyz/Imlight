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
 */

using Akka.Actor;
using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandQuest : CommandProtocol {

    internal override string Group { get; set; } = "quest";

    [Command("offer")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestOfferCommand(string questName) => Inform(CharacterActions.OfferQuest(OwnCharacter, questName));

    [Command("list")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestListCommand()
        => SendQuestAction("list", "");

    [Command("info")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestInfoCommand()
        => SendQuestAction("info", "");

    [Command("show")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestShowCommand([Remainder] string questName)
        => SendQuestAction("info", questName);

    [Command("completegoal")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestCompleteGoalCommand()
        => SendQuestAction("completegoal", "");

    [Command("completegoalfor")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestCompleteGoalForCommand([Remainder] string questName)
        => SendQuestAction("completegoal", questName);

    [Command("complete")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestCompleteCommand([Remainder] string questName)
        => SendQuestAction("complete", questName);

    [Command("grant")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestGrantCommand([Remainder] string questName)
        => SendQuestAction("grant", questName);

    [Command("forcegrant")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestForceGrantCommand([Remainder] string questName)
        => SendQuestAction("forcegrant", questName);

    [Command("remove")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestRemoveCommand([Remainder] string questName)
        => SendQuestAction("remove", questName);

    private void SendQuestAction(string action, string questName)
        => Context.SessionActor.Tell(new CHARACTER_103_PROTOCOL.MSG_QUESTCOMMAND {
            Action = action,
            QuestName = questName,
        });

}
