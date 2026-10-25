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
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.CrownShop;
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

    private const uint SoldEmoteTemplateId = 7001;
    private static readonly CustomEmoteBehaviorTemplate s_soldEmote = new() { m_bitFieldNumber = 40, m_emoteType = CustomEmoteType.CE_Emote };

    [Fact]
    public void Owns_DefaultEmote_WithoutPurchase() {
        var emote = new CustomEmoteBehaviorTemplate { m_bitFieldNumber = 3, m_isDefaultEmote = true };

        Assert.True(CustomEmoteOwnership.Owns(emote, 0, 0, 0));
    }

    [Fact]
    public void Owns_PurchasedBitInSecondWord() {
        Assert.True(CustomEmoteOwnership.Owns(s_soldEmote, 0, 1u << 8, 0));
        Assert.False(CustomEmoteOwnership.Owns(s_soldEmote, uint.MaxValue, 0, uint.MaxValue));
    }

    [Fact]
    public void RadialMenu_OwnedEmotesAndPhrases_AreStoredCanonical() {
        var menu = Menu((0, SoldEmoteTemplateId, 0), (1, 0, 55));
        byte[] raw = menu.ToByteString();

        Assert.True(CustomEmoteOwnership.TryAcceptRadialMenu(raw, maxSlots: 8, MayPlace(owned: true), out var canonical));
        Assert.Equal(raw, (byte[]) canonical);
    }

    [Fact]
    public void RadialMenu_UnownedSoldEmote_IsRejected() {
        var raw = Menu((0, SoldEmoteTemplateId, 0)).ToByteString();

        Assert.False(CustomEmoteOwnership.TryAcceptRadialMenu(raw, maxSlots: 8, MayPlace(owned: false), out _));
    }

    [Fact]
    public void RadialMenu_TrailingBytes_AreRejected() {
        byte[] raw = Menu((0, 0, 55)).ToByteString();

        Assert.False(CustomEmoteOwnership.TryAcceptRadialMenu(raw.Concat(new byte[] { 1, 2 }).ToArray(), maxSlots: 8, MayPlace(owned: true), out _));
    }

    [Fact]
    public void RadialMenu_TooManySlots_IsRejected() {
        var raw = Menu((0, 0, 1), (1, 0, 2), (2, 0, 3)).ToByteString();

        Assert.False(CustomEmoteOwnership.TryAcceptRadialMenu(raw, maxSlots: 2, MayPlace(owned: true), out _));
    }

    [Fact]
    public void RadialMenu_Empty_IsAccepted() {
        Assert.True(CustomEmoteOwnership.TryAcceptRadialMenu(default, maxSlots: 8, MayPlace(owned: false), out var canonical));
        Assert.Equal((byte[]) RadialMenuData.Empty, (byte[]) canonical);
    }

    private static System.Func<uint, bool> MayPlace(bool owned)
        => templateId => templateId != SoldEmoteTemplateId || owned;

    private static RadialMenuData Menu(params (short Slot, uint EmoteTemplateId, uint PhraseId)[] slots) => new() {
        Slots = slots.Select(s => new RadialMenuSlotInfo {
            m_slot = s.Slot,
            m_theEmoteTemplateID = s.EmoteTemplateId,
            m_menuChatPhraseChatID = s.PhraseId
        }).ToList()
    };

}
