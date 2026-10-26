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
 * NPC REFRESH GATE
 * ========================================================================
 * 
 * PURPOSE:
 * Decides when a session tells the NPCs around its player to re-send their
 * options: once per batch of quest changes, and never while a quest dialog is open.
 * 
 * USAGE EXAMPLE:
 * Owned by QuestService: Request on every quest change, TryTake when the
 * flush message arrives, DialogShown and DialogClosed around quest dialogs.
 * 
 * NOTE:
 * Re-sending options starts with MSG_LEAVESERVICERANGE, which may close a
 * dialog the NPC has open.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Services;

internal sealed class NpcRefreshGate {

    private readonly HashSet<string> _personaNames = [];
    private bool _everyNpc;
    private int _openDialogs;

    public bool IsWaitingForDialog => IsPending && _openDialogs > 0;

    public int OpenDialogs => _openDialogs;

    private bool IsPending => _everyNpc || _personaNames.Count > 0;

    public bool Request(string personaName = null) {
        // True when nothing was pending yet: the caller schedules one flush for the whole batch.
        var wasPending = IsPending;
        if (string.IsNullOrEmpty(personaName)) {
            _everyNpc = true;
        } else {
            _personaNames.Add(personaName);
        }

        return !wasPending;
    }

    public void DialogShown()
        => _openDialogs++;

    public bool DialogClosed(out IReadOnlyList<string> personaNames) {
        if (_openDialogs > 0) {
            _openDialogs--;
        }

        return TryTake(out personaNames);
    }

    public bool TakeAfterDialogTimeout(out IReadOnlyList<string> personaNames) {
        _openDialogs = 0;
        return TryTake(out personaNames);
    }

    public bool TryTake(out IReadOnlyList<string> personaNames) {
        // A null persona name means every NPC, which covers any named one.
        personaNames = [];
        if (!IsPending || _openDialogs > 0) {
            return false;
        }

        personaNames = _everyNpc ? [null] : [.. _personaNames];
        _everyNpc = false;
        _personaNames.Clear();
        return true;
    }

}
