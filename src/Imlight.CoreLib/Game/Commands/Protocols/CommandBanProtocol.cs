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

using Imlight.CoreLib.Game.AdminPanel;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandBanProtocol : CommandProtocol {

    internal override string Group { get; set; } = "ban";

    [Command("account")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("user", "username")]
    private void BanAccountCommand(string username, string time, [Remainder] string reason)
        => Inform(AccountModeration.Ban(Context.Account, Context.Character, Context.Actors, username, time, reason), true);

    [Command("machine")]
    [AuthRequired(AuthLevel.HallMonitor)]
    [Alias("pc", "computer")]
    private void BanMachineCommand(string machineId, string time, [Remainder] string reason)
        => Inform(AccountModeration.BanMachine(Context.Account, Context.Character, machineId, time, reason));

    [Command("ip")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void BanIpCommand(string ip, string time, [Remainder] string reason)
        => Inform(AccountModeration.BanIp(Context.Account, Context.Character, ip, time, reason));

    [Command("info")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void BanInfoCommand(string username) => Inform(AccountModeration.BanInfo(Context.Account, username), true);

}
