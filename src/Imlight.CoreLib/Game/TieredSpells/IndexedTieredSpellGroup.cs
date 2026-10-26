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
 * One tiered spell group of the index: the spell the group starts from, the shard reagent its upgrades cost
 * and every spell of the group.
 * 
 * USAGE EXAMPLE:
 * var shard = TieredSpellIndex.Instance.Groups[0].ShardTemplateId;
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.Game.TieredSpells;

/// <summary>
/// A tiered spell group as the index keeps it.
/// </summary>
/// <param name="TierOneSpell">The name of the group's tier-one spell.</param>
/// <param name="ShardTemplateId">The template id of the group's shard reagent.</param>
/// <param name="Spells">The names of every spell of the group, the tier-one spell included.</param>
internal sealed record IndexedTieredSpellGroup(string TierOneSpell, ulong ShardTemplateId, IReadOnlyList<string> Spells);
