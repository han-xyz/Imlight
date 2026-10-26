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
 * Builds the TieredSpellGroupVisibility the client reads to list the spellement groups a wizard can see.
 * 
 * USAGE EXAMPLE:
 * var visibility = TieredSpellVisibility.For(groups, trainedSpells, ownsReagent);
 * var data = TieredSpellVisibility.Serialize(visibility);
 * 
 * NOTE:
 * A group is visible when the wizard has trained one of its spells or owns its shard.
 * 
 * TODO:
 * - Multiple paths: doPrep stays 0.
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.IO;

namespace Imlight.CoreLib.Game.TieredSpells;

/// <summary>
/// The spellement groups a wizard can see.
/// </summary>
internal static class TieredSpellVisibility {

    private const byte Visible = 1;
    private const byte Hidden = 0;

    private static readonly ObjectSerializer s_serializer = new(Versionable: false);

    /// <summary>
    /// Builds one entry per group, in group order.
    /// </summary>
    /// <param name="groups">The groups in file order.</param>
    /// <param name="trainedSpells">The names of the spells the wizard has trained.</param>
    /// <param name="ownsReagent">Whether the wizard owns a reagent of a template id.</param>
    /// <param name="canUpgrade">Whether the wizard can upgrade a group now; null when nothing is upgradable.</param>
    /// <returns>The visibility, with nothing prepared for multiple paths.</returns>
    internal static TieredSpellGroupVisibility For(IReadOnlyList<IndexedTieredSpellGroup> groups,
        IReadOnlySet<string> trainedSpells, Func<ulong, bool> ownsReagent,
        Func<IndexedTieredSpellGroup, bool> canUpgrade = null)
        => new() {
            m_visibilityList = [.. groups.Select(group => IsVisible(group, trainedSpells, ownsReagent) ? Visible : Hidden)],
            m_canUpgradeList = [.. groups.Select(group => canUpgrade?.Invoke(group) == true ? 1 : 0)],
            m_doPrepTieredSpellForMPList = [.. groups.Select(_ => Hidden)],
        };

    /// <summary>
    /// Serialises a visibility for the Visibility field of MSG_CANSEETIEREDSPELLGROUPS.
    /// Throws when the serializer refuses it.
    /// </summary>
    /// <param name="visibility">The visibility to send.</param>
    /// <returns>The serialised visibility.</returns>
    internal static ByteString Serialize(TieredSpellGroupVisibility visibility)
        => s_serializer.Serialize(visibility, PropertyFlags.Prop_Public, out var data)
            ? data
            : throw new InvalidOperationException("Could not serialize TieredSpellGroupVisibility.");

    private static bool IsVisible(IndexedTieredSpellGroup group, IReadOnlySet<string> trainedSpells, Func<ulong, bool> ownsReagent)
        => ownsReagent(group.ShardTemplateId) || group.Spells.Any(trainedSpells.Contains);

}
