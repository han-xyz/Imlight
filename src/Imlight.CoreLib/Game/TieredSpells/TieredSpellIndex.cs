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
 * The tiered spell groups of the client's own data (TieredSpells.xml and TieredSpellsGroupInfo.xml): per group
 * its tier-one spell, its shard reagent and every spell of the group.
 * 
 * USAGE EXAMPLE:
 * var groups = TieredSpellIndex.Instance.Groups;
 * 
 * NOTE:
 * Groups keep file order: the client addresses a group by its position in TieredSpells.xml.
 * 
 * TODO:
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
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.TieredSpells;

/// <summary>
/// The tiered spell groups, read once from the root wad.
/// </summary>
internal sealed class TieredSpellIndex : RootSingleResourceSingleton<TieredSpellIndex>, IMemoryStreamDisposable {

    private const string GroupInfoResourceName = "TieredSpellsGroupInfo.xml";
    private const PropertyFlags ResourceFlags = PropertyFlags.Prop_Save;

    protected override string ResourceName => "TieredSpells.xml";

    /// <summary>
    /// Gets the groups in file order.
    /// </summary>
    internal IReadOnlyList<IndexedTieredSpellGroup> Groups { get; private set; }

    protected override void AfterLoad() {
        using var groupInfo = RootArchiveLoader.GetFileStream(GroupInfoResourceName);
        Groups = Read(Stream.ToArray(), groupInfo.ToArray());

        Logger.Information("Indexed {Count} tiered spell groups.", Logger.Args(Groups.Count));
    }

    /// <summary>
    /// Reads the groups from the two BINd files.
    /// Throws when either file does not decode.
    /// </summary>
    /// <param name="config">The bytes of TieredSpells.xml.</param>
    /// <param name="groupInfo">The bytes of TieredSpellsGroupInfo.xml.</param>
    /// <returns>The groups in file order.</returns>
    internal static IReadOnlyList<IndexedTieredSpellGroup> Read(byte[] config, byte[] groupInfo) {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize(config, ResourceFlags, out TieredSpellConfig spellConfig)) {
            throw new InvalidOperationException("Could not deserialize TieredSpells.xml.");
        }

        if (!serializer.Deserialize(groupInfo, ResourceFlags, out TieredSpellGroupInfoList infoList)) {
            throw new InvalidOperationException($"Could not deserialize {GroupInfoResourceName}.");
        }

        var spellsByGroup = (infoList.m_tieredSpellGroupInfoList ?? [])
            .ToLookup(info => info.m_theTieredSpellGroupInfoData.m_tsGroupIndex, info => info.m_spellName.ToString());

        return (spellConfig.m_tieredSpellGroupList ?? [])
            .Select((group, index) => new IndexedTieredSpellGroup(
                group.m_tierOneSpell.ToString(),
                group.m_shardType.Full,
                [.. spellsByGroup[index]]))
            .ToList();
    }

    public void DisposeStream() => Stream.Dispose();

}
