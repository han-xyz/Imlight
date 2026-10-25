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
 * Grants a ResLoot result's loot to the interacting player and shows the
 * loot popup, the way a wooden chest does.
 * 
 * USAGE EXAMPLE:
 * Runs from a generic chest's interact result list.
 * 
 * NOTE:
 * The client data names a server-side loot table (field 0x975DE361) that
 * Imcodec does not decode, so m_lootTable is null for every chest in the
 * root wad, and the wooden chest gold roll is granted on top of any table.
 * A named zone object or trigger grants once per character per zone instance.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResLootHandler : BaseResultHandler<ResLoot> {

    private const float QueryWizardTimeoutSeconds = 5.0f;
    private static int s_loggedGoldFallback;

    public override bool Execute(IResultContext context) {
        var wizard = QueryWizard(context.GetPlayerRef());
        if (wizard is null) {
            Logger.Warning("ResLoot could not retrieve the player's wizard.");

            return false;
        }

        // Results without a named source (quest rewards) are not claimed. InteractObjectStateComponent already
        // stops offering a chest's option again within a zone instance; the claim also holds it per character.
        var sourceName = context.GetSourceName();
        var zoneState = context.GetZoneState();
        if (!string.IsNullOrEmpty(sourceName) && zoneState?.TryClaimLoot(sourceName, wizard.CharId) == false) {
            Logger.Debug("ResLoot from {SourceName} was already granted to character {CharacterId}.",
                Logger.Args(sourceName, wizard.CharId));

            return true;
        }

        if (Result.m_lootTable is null && Interlocked.Exchange(ref s_loggedGoldFallback, 1) == 0) {
            Logger.Debug("ResLoot without a decoded loot table; gold fallback.");
        }

        LootGranter.GrantAndDisplay(context.GetPlayerRef(), wizard, LootFor(Result.m_lootTable), context.GetXpScale());

        return true;
    }

    internal static DropTableResult LootFor(LootInfoList lootTable)
        => new() {
            GoldAmount = WoodenChestComponent.RollGold() + (lootTable?.m_goldInfo?.m_goldAmount ?? 0),
            ExperienceAmount = lootTable?.m_loot?.OfType<MagicXPLootInfo>().Sum(x => x.m_experience) ?? 0,
            Items = lootTable?.m_loot?
                .OfType<ItemLootInfo>()
                .Select(x => new DropItemResult {
                    ItemId = x.m_itemID.Full.ToString(CultureInfo.InvariantCulture),
                    Quantity = x.m_numItems,
                })
                .ToList() ?? [],
        };

    private static Wizard QueryWizard(IActorRef playerRef) {
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
