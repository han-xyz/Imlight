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
 * A wizard's progress in one tiered spell group: the tier in the spellbook and the tiers bought with shards,
 * each with the shards it cost, so a refund returns what was paid.
 *
 * USAGE EXAMPLE:
 * var progress = wizard.TieredSpells.Find(p => p.GroupIndex == groupIndex);
 *
 * NOTE:
 * Saved with the wizard document; the unlocks form one path from the tier-one spell to the current tier.
 *
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.Player;

public sealed class TieredSpellProgress {

    public int GroupIndex { get; set; }
    public uint TierOneSpellId { get; set; }
    public uint CurrentSpellId { get; set; }
    public List<TieredSpellUnlock> Unlocks { get; set; } = [];
    public bool DonePrepForMultiplePaths { get; set; }

}

public sealed class TieredSpellUnlock {

    public uint SpellId { get; set; }
    public uint FromSpellId { get; set; }
    public int ShardsSpent { get; set; }

}
