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
 * EMOTE TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for what a player's emote may show to the zone: the
 * moderated emote text, and only emotes the player owns.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using Imlight.CoreLib.Game.Services;
using Xunit;

namespace Imlight.Tests;

public sealed class EmoteTests {

    [Fact]
    public void EmoteText_IsCleanedLikeChat() {
        Assert.Equal("Hello there!", EmoteService.EmoteTextForBroadcast("\u0007Hello there!\u202E ", isMuted: false));
    }

    [Fact]
    public void EmoteText_FromMutedPlayer_IsDropped() {
        Assert.Equal("", EmoteService.EmoteTextForBroadcast("Hello there!", isMuted: true));
    }

}
