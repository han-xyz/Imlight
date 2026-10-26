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
 * SPELLEMENT SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Answers the requests of the Spellements page: which tiered spell groups the wizard can see, and the empty
 * done-prep and marked-as-untrained lists.
 *
 * USAGE EXAMPLE:
 * The client sends MSG_CANSEETIEREDSPELLGROUPS, MSG_REQUESTTSDONEPREPFORMP and MSG_REQUESTTSMARKEDASUNTRAINED
 * when the page opens; each is answered once. The server never pushes.
 *
 * NOTE:
 *
 * TODO:
 * - Upgrades, refunds and multiple paths.
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.TieredSpells;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class TieredSpellService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TieredSpellService(parentActor));

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_CANSEETIEREDSPELLGROUPS))]
    private void ReceiveCanSeeTieredSpellGroups(WIZARD2_53_PROTOCOL.MSG_CANSEETIEREDSPELLGROUPS message) {
        Logger.Debug("Tiered spell group visibility requested with {Visibility}, shard type {ShardType}.",
            Logger.Args(Convert.ToHexString(message.Visibility), message.ShardType));

        var wizard = GetActiveWizard();
        var groups = TieredSpellIndex.Instance.Groups;
        var visibility = TieredSpellVisibility.For(groups, TrainedSpellNames(wizard), wizard.AlchemyBehavior.HasReageant);
        var data = TieredSpellVisibility.Serialize(visibility);

        Logger.Debug("Tiered spell group visibility for {Count} groups is {Length} bytes.",
            Logger.Args(groups.Count, data.Length));

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_CANSEETIEREDSPELLGROUPS {
            Visibility = data,
            CheckTieredSpellNotifications = message.CheckTieredSpellNotifications,
            ShardType = message.ShardType,
        });
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTTSDONEPREPFORMP))]
    private void ReceiveRequestDonePrepForMultiplePaths(WIZARD3_56_PROTOCOL.MSG_REQUESTTSDONEPREPFORMP _)
        => SendToSocket(new WIZARD3_56_PROTOCOL.MSG_UPDATETSDONEPREPFORMP { Data = string.Empty });

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTTSMARKEDASUNTRAINED))]
    private void ReceiveRequestMarkedAsUntrained(WIZARD3_56_PROTOCOL.MSG_REQUESTTSMARKEDASUNTRAINED _)
        => SendToSocket(new WIZARD3_56_PROTOCOL.MSG_UPDATETSMARKEDASUNTRAINED { Data = string.Empty });

    private static HashSet<string> TrainedSpellNames(Wizard wizard) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var templateId in wizard.SpellbookBehavior.LearnedSpellTemplateIds) {
            if (CoreObjectFactory.GetCoreTemplate(templateId) is SpellTemplate spell) {
                names.Add(spell.m_name.ToString());
            }
        }

        return names;
    }

}
