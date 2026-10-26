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
 * SERVICE OPTION TABLE
 * ========================================================================
 * 
 * PURPOSE:
 * Remembers, per player, which service component owns each option index of
 * the NPC option list that player was last sent.
 * 
 * USAGE EXAMPLE:
 * Owned by InteractServiceMementoComponent: Build when options are sent,
 * TryRoute when the player clicks one, Remove when the player leaves.
 * 
 * NOTE:
 * Players near the same NPC can be offered different options (a quest for one,
 * not the other), so an index only means something against that player's list.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class ServiceOptionTable {

    private readonly Dictionary<ulong, List<(IServiceComponent Owner, uint LocalIndex, ServiceOptionBase Option)>> _routesByPlayer = [];

    public List<ServiceOptionBase> Build(
        ulong playerId,
        IEnumerable<(IServiceComponent Component, List<ServiceOptionBase> Options)> componentOptions) {
        var allOptions = new List<ServiceOptionBase>();
        var routes = new List<(IServiceComponent Owner, uint LocalIndex, ServiceOptionBase Option)>();
        foreach (var (component, options) in componentOptions) {
            for (var i = 0; i < options.Count; i++) {
                // The client echoes m_serviceIndex on a click, so it has to be the flat index routed below.
                options[i].m_serviceIndex = (uint) allOptions.Count;
                routes.Add((component, (uint) i, options[i]));
                allOptions.Add(options[i]);
            }
        }

        _routesByPlayer[playerId] = routes;
        return allOptions;
    }

    public bool TryRoute(ulong playerId, uint serviceIndex, out IServiceComponent owner, out uint localIndex) {
        owner = null;
        localIndex = 0;
        if (!_routesByPlayer.TryGetValue(playerId, out var routes) || serviceIndex >= routes.Count) {
            return false;
        }

        (owner, localIndex, _) = routes[(int) serviceIndex];
        return true;
    }

    public bool TryGetSentOption(ulong playerId, IServiceComponent owner, uint localIndex, out ServiceOptionBase option) {
        option = null;
        if (!_routesByPlayer.TryGetValue(playerId, out var routes)) {
            return false;
        }

        var route = routes.Find(r => ReferenceEquals(r.Owner, owner) && r.LocalIndex == localIndex);
        option = route.Option;
        return option is not null;
    }

    public bool TryFindServiceIndex(ulong playerId, IServiceComponent owner, out uint serviceIndex) {
        serviceIndex = 0;
        var index = _routesByPlayer.TryGetValue(playerId, out var routes)
            ? routes.FindIndex(r => ReferenceEquals(r.Owner, owner))
            : -1;
        if (index < 0) {
            return false;
        }

        serviceIndex = (uint) index;
        return true;
    }

    public void Remove(ulong playerId)
        => _routesByPlayer.Remove(playerId);

}
