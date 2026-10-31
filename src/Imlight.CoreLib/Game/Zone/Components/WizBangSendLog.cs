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
 * WIZBANG SEND LOG
 * ========================================================================
 *
 * PURPOSE:
 * Decides, per player, whether an entity's wizbang has to be sent again. The memento re-evaluates every
 * second; sending every evaluation streamed hundreds of MSG_WIZBANG a second to each player in a street, which
 * the client shows as a flicker.
 *
 * USAGE EXAMPLE:
 * if (log.ShouldSend(playerActor, wizBang)) { playerActor.Tell(...); }   log.Forget(playerActor) when they leave.
 *
 * NOTE:
 * A value is sent twice (the second send covers a client still building the scene), then only when it changes.
 * A forgotten player starts over.
 *
 * TODO:
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System.Collections.Generic;
using Imlight.CoreLib.Game.WizBang;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class WizBangSendLog {

    private readonly Dictionary<object, (WizBangs WizBang, bool Confirmed)> _sent = [];

    /// <summary>
    /// Whether to send <paramref name="wizBang"/> to the player now, and records the send when it says yes.
    /// </summary>
    public bool ShouldSend(object player, WizBangs wizBang) {
        if (!_sent.TryGetValue(player, out var last)) {
            _sent[player] = (wizBang, false);

            return true;
        }

        if (last.WizBang != wizBang) {
            _sent[player] = (wizBang, false);

            return true;
        }

        if (!last.Confirmed) {
            _sent[player] = (wizBang, true);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Drops what was sent to a player who left the entity's range or the zone.
    /// </summary>
    public void Forget(object player)
        => _sent.Remove(player);

}
