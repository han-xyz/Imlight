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
 * RESULT HANDLERS
 * ========================================================================
 * 
 * PURPOSE:
 * Accepts the results the server does not act on, so the rest of a result
 * list runs: client-only ones (package downloads, zone warnings, mission
 * doors, cinematics) and untracked ones.
 * 
 * USAGE EXAMPLE:
 * Runs from quest, goal and zone result lists.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResDownloadPackageHandler : BaseResultHandler<ResDownloadPackage> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Package download of {PackageCount} packages left to the client.",
            Logger.Args(Result.m_packageList?.Count ?? 0));

        return true;
    }

}

internal sealed class ResDownloadElementHandler : BaseResultHandler<ResDownloadElement> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Element download {PackageList} left to the client.", Logger.Args(Result.m_elementPackageList));

        return true;
    }

}

internal sealed class ResMarkZoneNoWarnHandler : BaseResultHandler<ResMarkZoneNoWarn> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Zone download warning mark left to the client.");

        return true;
    }

}

internal sealed class ResTimeStampEntryHandler : BaseResultHandler<ResTimeStampEntry> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Registry timestamp {RegistryEntry} not recorded.", Logger.Args(Result.m_registryEntry));

        return true;
    }

}

internal sealed class ResAddMissionDoorHandler : BaseResultHandler<ResAddMissionDoor> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Mission door add not supported.");

        return true;
    }

}

internal sealed class ResRemoveMissionDoorHandler : BaseResultHandler<ResRemoveMissionDoor> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Mission door removal not supported.");

        return true;
    }

}

internal sealed class ResCinematicHandler : BaseResultHandler<ResCinematic> {

    public override bool Execute(IResultContext context) {
        Logger.Debug("Cinematic {CinematicName} not played.", Logger.Args(Result.m_cinematicName));

        return true;
    }

}
