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
 * REQUIREMENT QUERY
 * ========================================================================
 *
 * PURPOSE:
 * Reads a requirement the client asks the server to check (MSG_REQASKSERVER).
 *
 * USAGE EXAMPLE:
 * ClientService reads every MSG_REQASKSERVER with RequirementQuery.TryDecode.
 *
 * NOTE:
 * The client sends one requirement per message, most of them ReqHasEntry checks
 * of quest completion entries, and sends them in batches of hundreds
 * when the player's quests change.
 *
 * TODO:
 * - How does the reply's Response encode the answer? Is m_applyNOT applied by
 *   the client or the server?
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements;

internal static class RequirementQuery {

    // Every property of ReqHasEntry is Public; none of its own properties is Transmit.
    private const uint PropertyMask = (uint) PropertyFlags.Prop_Public;

    internal static bool TryDecode(byte[] serialized, out Requirement requirement) {
        requirement = null;
        if (serialized is null || serialized.Length == 0) {
            return false;
        }

        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);
        try {
            if (serializer.Deserialize<PropertyClass>(serialized, PropertyMask, out var decoded) && decoded is Requirement found) {
                requirement = found;

                return true;
            }
        }
        catch (Exception) {
            // A requirement the serializer cannot read is reported as unreadable.
        }

        return false;
    }

}
