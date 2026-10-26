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
 * ZONE ARRIVAL
 * ========================================================================
 *
 * PURPOSE:
 * Holds the mount reconcile of a zone arrival until the client has built
 * its player object, so the equip messages reach an existing player.
 *
 * USAGE EXAMPLE:
 * ZoneService calls PlayerAdded on MSG_ADDPLAYERRSP, ClientZoned on
 * MSG_CLIENTZONED and FallbackElapsed when the client never sends it.
 *
 * NOTE:
 * The client builds its own player from MSG_LOGINCOMPLETE only after the
 * zone has loaded, then sends MSG_CLIENTZONED.
 *
 * TODO:
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;

namespace Imlight.CoreLib.Game.World;

internal sealed class ZoneArrival(Action reconcileMount) {

    private bool _isAwaitingClient;

    public void PlayerAdded() => _isAwaitingClient = true;

    public void ClientZoned() => Release();

    public void FallbackElapsed() => Release();

    private void Release() {
        if (!_isAwaitingClient) {
            return;
        }

        _isAwaitingClient = false;
        reconcileMount();
    }
}
