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
 * FORCE PLAYER AS PET RESULT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Puts the player into play-as-pet mode from a zone trigger (the WC_OldeTown pet tower fires it when the
 * player steps up to the pet-only door), as if they had pressed the play-as-pet button themselves.
 *
 * USAGE EXAMPLE:
 * The result executor runs it for every ResForcePlayerAsPet of a fired trigger; nothing calls it directly.
 *
 * NOTE:
 * The result names the pet the zone lends the player (the pet tower's quest pet); it need not be equipped. A
 * template of 0 means the equipped pet, and without one the result fails and the player stays a wizard. The
 * whole zone hears the switch, the player included, since the client did not start it.
 *
 * TODO:
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResForcePlayerAsPetHandler : BaseResultHandler<ResForcePlayerAsPet> {

    private const byte ENTERING = 1;
    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;

    public override bool Execute(IResultContext context) {
        var queryTimeout = TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS);
        var queryResponse = context
            .GetPlayerRef()
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(), queryTimeout).Result;
        if (queryResponse?.Wizard is not Wizard wizard) {
            Logger.Error("ResForcePlayerAsPet could not read the character within {Seconds} seconds.",
                Logger.Args(QUERY_WIZARD_TIMEOUT_SECONDS));

            return false;
        }

        var petOwner = wizard.PetOwnerBehavior;
        var petTemplate = Result.m_petTemplate != 0 ? Result.m_petTemplate : (uint) petOwner.EquippedPetTemplateId;
        if (petTemplate == 0) {
            Logger.Warning("ResForcePlayerAsPet names no pet template and {Wizard} has no pet equipped, so they stay a wizard.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName()));

            return false;
        }

        petOwner.PlayAsPet();
        Logger.Debug("{Wizard} plays as pet template {PetTemplate} on the zone's demand.",
            Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), petTemplate));

        context.GetZoneActor().Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new WIZARD2_53_PROTOCOL.MSG_SWITCHTOPET {
                GlobalID = context.GetPlayerObj().m_globalID,
                Status = ENTERING,
                PetTemplate = petTemplate
            },
            Sender = context.GetPlayerRef(),
            Selfless = false,
            Targets = ZoneBroadcastTarget.Players
        });

        return true;
    }

}
