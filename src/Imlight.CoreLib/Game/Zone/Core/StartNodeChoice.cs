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
 * START NODE CHOICE
 * ========================================================================
 *
 * PURPOSE:
 * Picks the path node a spawned creature starts at, by the spawn item's start node type. "Random unique" takes a
 * node no living creature of the path started at, so a spawner that fills up (the wisps, twenty to a path) does
 * not stack its creatures on one spot; identical glowing sprites in one place flicker on the client.
 *
 * USAGE EXAMPLE:
 * var node = StartNodeChoice.Choose(info.m_kStartNodeType, info.m_startNode, nodes, occupied, rng);  // null: no free node
 *
 * NOTE:
 * "Specific" names a node by id; an unknown id falls back to the first node.
 *
 * TODO:
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Core;

internal static class StartNodeChoice {

    /// <summary>
    /// The node to start at, or null when the type wants a free node and every node is taken.
    /// </summary>
    /// <param name="type">The spawn item's start node rule.</param>
    /// <param name="startNode">The node id a specific rule names.</param>
    /// <param name="nodes">The path's nodes, in path order.</param>
    /// <param name="occupied">The start nodes of the path's living creatures.</param>
    /// <param name="rng">The random source.</param>
    public static NodeObject Choose(StartNodeType type, uint startNode, IReadOnlyList<NodeObject> nodes,
                                    IEnumerable<NodeObject> occupied, Random rng) {
        if (nodes.Count == 0) {
            return null;
        }

        switch (type) {
            case StartNodeType.SNT_RANDOM_UNIQUE:
                var taken = occupied.Select(NodeId).ToHashSet();
                var free = nodes.Where(node => !taken.Contains(NodeId(node))).ToList();

                return free.Count == 0 ? null : free[rng.Next(free.Count)];
            case StartNodeType.SNT_FIRST:
                return nodes[0];
            case StartNodeType.SNT_LAST:
                return nodes[^1];
            case StartNodeType.SNT_SPECIFIC:
                return nodes.FirstOrDefault(node => NodeId(node) == startNode) ?? nodes[0];
            default:
                return nodes[rng.Next(nodes.Count)];
        }
    }

    private static uint NodeId(NodeObject node)
        => Convert.ToUInt32(node.m_id);

}
