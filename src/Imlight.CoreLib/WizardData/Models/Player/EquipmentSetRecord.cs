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
 * EQUIPMENT SET RECORD
 * ========================================================================
 * 
 * PURPOSE:
 * One saved equipment set of a wizard: the set name the client picked and the items equipped when it was saved.
 * 
 * USAGE EXAMPLE:
 * var set = wizard.EquipmentSets.Find(s => s.Name == name);
 * 
 * NOTE:
 * Saved with the wizard document. Name is the client's encoded set name (EquipmentSetNames wheel indexes).
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.Player;

public sealed class EquipmentSetRecord {

    public uint Name { get; set; }
    public List<EquipmentSetItem> Items { get; set; } = [];

}

public sealed class EquipmentSetItem {

    public ulong ItemId { get; set; }
    public string SlotName { get; set; }

}
