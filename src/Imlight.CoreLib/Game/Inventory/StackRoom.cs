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
 * STACK ROOM
 * ========================================================================
 * 
 * PURPOSE:
 * Checks whether a wizard's reagent and pet snack bags can take a set of stacks at once.
 * 
 * USAGE EXAMPLE:
 * PackManager, CrownShopService and GiftingService before a pack, bundle or gift is granted.
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
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Inventory;

internal static class StackRoom {

    /// <summary>
    /// Whether the reagent and pet snack bags can take every stack listed; other templates are ignored.
    /// </summary>
    internal static bool CanHoldStacks(Wizard wizard, IEnumerable<(ulong TemplateId, CoreTemplate Template, int Count)> items) {
        var reagents = new Dictionary<ulong, int>();
        var snacks = new Dictionary<ulong, int>();
        foreach (var (templateId, template, count) in items) {
            if (template is ReagentItemTemplate) {
                reagents[templateId] = reagents.GetValueOrDefault(templateId) + count;
            }
            else if (template is PetSnackItemTemplate) {
                snacks[templateId] = snacks.GetValueOrDefault(templateId) + count;
            }
        }

        return reagents.All(reagent => wizard.AlchemyBehavior?.HasReagentSpace(reagent.Key, reagent.Value) ?? false)
            && snacks.All(snack => wizard.PetSnackBehavior?.HasSnackSpace(snack.Key, snack.Value) ?? false);
    }

    internal static IEnumerable<(ulong TemplateId, CoreTemplate Template, int Count)> BundleContents(ItemBundleTemplate bundle, int count)
        => bundle.m_bundleItems.Select(templateId => ((ulong) templateId, CoreObjectFactory.GetCoreTemplate(templateId), count));

}
