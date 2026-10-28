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
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.AdminPanel;

internal static partial class CharacterActions {

    private static readonly uint[] s_cantripSpellIds = [ 1964703872, 1462882588, 290925625, 1521398842, 189772023,
        1767938148, 536458141, 1189939434, 717871356, 999765474, 1494670771, 577368445, 1672654002, 1799150966,
        1282482033, 1884951148, 467451672, 1419042141, 220931873, 466468632, 2083836762, 1992492243 ];

    #region Spellbook

    internal static AdminActionResult LearnSpell(CharacterTarget target, string spellTemplateId) {
        if (!TryGetSpellTemplate(spellTemplateId, out var templateId, out var spellName)) {
            return Refuse("Invalid spell template ID.");
        }

        var spell = SpellFactory.GetSpell(templateId);
        if (spell is null) {
            return Refuse("Invalid spell template ID.");
        }

        if (!target.Wizard.LearnSpell(spell)) {
            return Refuse("Failed to learn spell. The spell may already be known.");
        }

        target.Tell(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK { SpellID = (int) templateId });

        return Done($"Learned the spell {spellName}.");
    }

    internal static AdminActionResult UnlearnSpell(CharacterTarget target, string spellTemplateId) {
        if (!TryGetSpellTemplate(spellTemplateId, out var templateId, out var spellName)) {
            return Refuse("Invalid spell template ID.");
        }

        if (!target.Wizard.UnlearnSpell(templateId)) {
            return Refuse("Failed to unlearn spell. The spell may not be known.");
        }

        target.Tell(new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMBOOK { SpellID = (int) templateId });

        return Done($"Unlearned the spell {spellName}.");
    }

    internal static AdminActionResult UnlearnAllSpells(CharacterTarget target) {
        var wizard = target.Wizard;
        if (wizard.SpellbookBehavior.LearnedSpellTemplateIds is not { Count: > 0 } learned) {
            return Done("No Spells To Unlearn");
        }

        var lines = new List<string>();
        foreach (var spell in learned.ToList()) {
            target.Tell(new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMBOOK { SpellID = (int) spell });
            wizard.SpellbookBehavior.RemoveSpellFromBook(spell);

            // Persistent save.
            WizardCollection.UnlearnSpell(wizard, spell);
            lines.Add($"Unlearned the spell {spell}.");
        }

        return AdminActionResult.Done($"Unlearned {lines.Count} spells.", lines);
    }

    internal static AdminActionResult LearnAllCantrips(CharacterTarget target) {
        foreach (var spellId in s_cantripSpellIds) {
            var spell = SpellFactory.GetSpell(spellId);
            if (spell is null || !target.Wizard.LearnSpell(spell)) {
                continue;
            }

            target.Tell(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK { SpellID = (int) spellId });
        }

        return Done("Learned all cantrips.");
    }

    // Temporary spells live only in the session, so they need the character online.
    internal static AdminActionResult AddTemporarySpell(CharacterTarget target, string spellTemplateId) {
        if (!uint.TryParse(spellTemplateId, out var templateId) || SpellFactory.GetSpell(templateId) is not { } spell) {
            return Refuse("Invalid spell template ID.");
        }

        target.Wizard.AddTemporarySpell(spell);

        return Done("Added the spell.");
    }

    internal static AdminActionResult RemoveTemporarySpell(CharacterTarget target, string spellTemplateId) {
        if (!uint.TryParse(spellTemplateId, out var templateId) || SpellFactory.GetSpell(templateId) is null) {
            return Refuse("Invalid spell template ID.");
        }

        target.Wizard.RemoveTemporarySpell(templateId);

        return Done("Removed the spell.");
    }

    private static bool TryGetSpellTemplate(string spellTemplateId, out uint templateId, out string spellName) {
        spellName = null;
        if (!uint.TryParse(spellTemplateId, out templateId)
            || CoreObjectFactory.GetCoreTemplate(templateId) is not SpellTemplate spellTemplate) {
            return false;
        }

        spellName = spellTemplate.m_name;

        return true;
    }

    #endregion

    #region Dynamods

    internal static AdminActionResult ListDynamods(CharacterTarget target) {
        var dynamods = target.Wizard.DynamodSet?.Dynamods?.Where(d => d is not null).ToList() ?? [];
        if (dynamods.Count == 0) {
            return Done("No dynamods on this character.");
        }

        return AdminActionResult.Done($"Dynamods: {dynamods.Count}",
            [.. dynamods.Select(d => $"{d.ClientTag} = {d.ModState}")]);
    }

    internal static AdminActionResult SetDynamod(CharacterTarget target, string state, string clientTag) {
        var normalizedState = state?.Trim().ToLowerInvariant() switch {
            "on" => "On",
            "off" => "Off",
            _ => null
        };
        if (normalizedState is null) {
            return Refuse("Invalid state. Use 'on' or 'off'.");
        }

        if (string.IsNullOrWhiteSpace(clientTag)) {
            return Refuse("Enter the client tag.");
        }

        AddDynamod(target, clientTag, normalizedState);

        return Done($"Dynamod '{clientTag}' set to {normalizedState}.");
    }

    internal static AdminActionResult ToggleDynamod(CharacterTarget target, string clientTag) {
        if (string.IsNullOrWhiteSpace(clientTag)) {
            return Refuse("Enter the client tag.");
        }

        var currentState = target.Wizard.DynamodSet?.Dynamods?
            .FirstOrDefault(d => d is not null && d.ClientTag.Equals(clientTag, StringComparison.OrdinalIgnoreCase))?
            .ModState;
        var newState = string.Equals(currentState, "Off", StringComparison.OrdinalIgnoreCase) ? "On" : "Off";

        AddDynamod(target, clientTag, newState);

        return Done($"Dynamod '{clientTag}' toggled to {newState} (was {currentState ?? "not present"}).");
    }

    internal static AdminActionResult RemoveDynamod(CharacterTarget target, string clientTag) {
        if (string.IsNullOrWhiteSpace(clientTag)) {
            return Refuse("Enter the client tag.");
        }

        if (target.Online) {
            target.Tell(new CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD {
                DynaMod = new ResRemoveDynaMod { m_dynaModClientTag = clientTag },
                ContextActor = target.Session
            });
        }
        else {
            target.Wizard.RemoveDynamod(clientTag);
        }

        return Done($"Dynamod '{clientTag}' removed.");
    }

    // A live character's dynamod service also tells the player and the zone; offline the mod is only saved.
    private static void AddDynamod(CharacterTarget target, string clientTag, string state) {
        if (!target.Online) {
            target.Wizard.AddDynamod("", clientTag, state);

            return;
        }

        target.Tell(new CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD {
            DynaMod = new ResAddDynaMod {
                m_dynaModClientTag = clientTag,
                m_dynaModState = state,
                m_zoneName = ""
            },
            ContextActor = target.Session
        });
    }

    #endregion

}
