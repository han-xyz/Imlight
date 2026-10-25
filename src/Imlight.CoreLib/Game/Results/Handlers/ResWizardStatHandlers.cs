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
 * RESULT HANDLERS
 * ========================================================================
 * 
 * PURPOSE:
 * Grants the wizard stat results whose amount the data does not carry:
 * quest training points and the gardening unlock.
 * 
 * USAGE EXAMPLE:
 * Runs from the Explorer quests' end results (Explorer-001-4) and the
 * gardening starter quest (WC-GRDN-C02-002).
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResAddTrainingPointsHandler : BaseResultHandler<ResAddTrainingPoints> {

    // ResAddTrainingPoints carries no amount; retail Zeke/Explorer quests grant one.
    private const int TrainingPointsPerResult = 1;

    public override bool Execute(IResultContext context) {
        if (ActiveWizardQuery.Query(context.GetPlayerRef()) is not Wizard wizard) {
            Logger.Warning("ResAddTrainingPoints could not retrieve the player's wizard.");

            return false;
        }

        var trainingPoints = TrainingPointsAfterGrant(wizard.MagicSchoolBehavior.TrainingPoints);
        wizard.UpdateTrainingPoints(trainingPoints);
        context.GetPlayerRef().Tell(new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING {
            TrainingPoints = trainingPoints
        });

        return true;
    }

    internal static int TrainingPointsAfterGrant(int trainingPoints) => trainingPoints + TrainingPointsPerResult;

}

internal sealed class ResSetGardeningLevelHandler : BaseResultHandler<ResSetGardeningLevel> {

    private const byte StartingGardeningLevel = 1;

    public override bool Execute(IResultContext context) {
        if (ActiveWizardQuery.Query(context.GetPlayerRef()) is not Wizard wizard) {
            Logger.Warning("ResSetGardeningLevel could not retrieve the player's wizard.");

            return false;
        }

        if (wizard.GameStats.m_gardeningLevel == 0) {
            wizard.GameStats.m_gardeningLevel = StartingGardeningLevel;
            Logger.Debug("Gardening level set to {GardeningLevel} in memory only.", Logger.Args(StartingGardeningLevel));
        }

        return true;
    }

}

internal static class ActiveWizardQuery {

    private const float QueryWizardTimeoutSeconds = 5.0f;

    public static Wizard Query(IActorRef playerRef) {
        try {
            return playerRef
                .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(),
                    TimeSpan.FromSeconds(QueryWizardTimeoutSeconds))
                .Result?.Wizard;
        } catch (AggregateException ex) when (ex.InnerException is AskTimeoutException) {
            return null;
        }
    }

}
