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
 * ZONE ACCESS
 * ========================================================================
 * 
 * PURPOSE:
 * Keeps accounts at AuthLevel.None out of QA zones (a path containing "QA_"), whatever sends them there.
 * 
 * USAGE EXAMPLE:
 * if (!ZoneAccess.Admits(account, message.DestinationZone)) { return; }
 * 
 * NOTE:
 * A refused transfer leaves the player in the current zone.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.World;

internal static class ZoneAccess {

    private const string QaZoneMarker = "QA_";

    /// <summary>
    /// Whether the account may enter the zone; a refusal is logged as a warning.
    /// </summary>
    internal static bool Admits(Account account, string zonePath) {
        if (IsAllowed(account.AuthLevel, zonePath)) {
            return true;
        }

        Logger.Warning("Account {Username} ({AuthLevel}) was refused a transfer to QA zone {Zone}.",
            Logger.Args(account.Username, account.AuthLevel, zonePath));

        return false;
    }

    internal static bool IsAllowed(AuthLevel authLevel, string zonePath)
        => authLevel > AuthLevel.None || !zonePath.Contains(QaZoneMarker, StringComparison.Ordinal);

}
