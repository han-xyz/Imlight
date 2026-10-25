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

using System.Collections.Concurrent;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Resources;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Components;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.MessageLayer.Generated;
using Imcodec.Types;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResPlaySoundHandler : BaseResultHandler<ResPlaySound> {

    // The named sound templates (e.g. ObjectData/ChestOpenSound.xml) use a class Imcodec cannot deserialize.
    private static readonly Dictionary<string, uint> s_soundTemplateIdsByName = new() {
        ["Wood Chest Opened"] = WoodenChestComponent.OpenSoundTemplateId,
    };
    private static readonly ConcurrentDictionary<string, byte> s_unresolvedSoundNames = new();

    public override bool Execute(IResultContext context) {
        string soundName = Result.m_soundName;
        if (string.IsNullOrEmpty(soundName) || s_unresolvedSoundNames.ContainsKey(soundName)) {
            return true;
        }

        if (!TryResolveTemplateId(soundName, out var templateId)) {
            if (s_unresolvedSoundNames.TryAdd(soundName, 0)) {
                Logger.Warning("Sound {SoundName} has no known template ID; it will not play.",
                    Logger.Args(soundName));
            }

            return true;
        }

        var msg = new GAME_5_PROTOCOL.MSG_PLAYSOUND { SoundID = new GID(templateId) };
        context.GetPlayerRef().Tell(msg);

        return true;
    }

    private static bool TryResolveTemplateId(string soundName, out uint templateId)
        => s_soundTemplateIdsByName.TryGetValue(soundName, out templateId)
           || CoreObjectFactory.TryGetCoreTemplateID(x => x.m_filename.ToString().Contains(soundName), out templateId);

}
