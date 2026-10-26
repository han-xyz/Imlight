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
 * NETWORKING
 * ========================================================================
 * 
 * PURPOSE:
 * Who hears a zone-wide chat line: everyone in the zone but the sender and the players who block the sender.
 * 
 * USAGE EXAMPLE:
 * MessageService.SendFilteredZoneMessage, used by local, radial and quick chat.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.WizardData.Models.Misc;

namespace Imlight.CoreLib.Shared.Networking;

internal static class ZoneChatRecipients {

    public static IEnumerable<OnlinePlayer> Select(IEnumerable<OnlinePlayer> playersInZone, ulong senderCharId,
                                                   IReadOnlyCollection<ulong> blockedBy)
        => playersInZone.Where(player => player.CharacterId != senderCharId && !blockedBy.Contains(player.CharacterId));

}
