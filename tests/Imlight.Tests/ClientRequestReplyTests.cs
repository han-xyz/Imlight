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
 * CLIENT REQUEST REPLY TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the replies to the client's standing ladder and mail check requests.
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
 * Last Updated: 10/27/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class ClientRequestReplyTests {

    [Fact]
    public void LadderReplyIsEmptyForTheAskedCharacter() {
        var ladder = PvPRatingService.EmptyLadder(42);

        Assert.Equal(42UL, ladder.CharacterID);
        Assert.True(ladder.LadderData.IsEmpty);
        Assert.Equal(0U, ladder.TourneyCredits);
        Assert.Equal(0U, ladder.TourneyHostingCredits);
    }

    [Fact]
    public void MailCheckWithGiftsInTheMailboxAnnouncesNewMail() {
        var wizard = new Wizard { CharId = 7, Account = new Account { GiftMailbox = [new Mail(), new Mail()] } };

        var notice = GiftingService.NewMailNotice(wizard);

        Assert.NotNull(notice);
        Assert.Equal(7UL, notice.CharacterID);
    }

    [Fact]
    public void MailCheckWithAnEmptyMailboxAnnouncesNothing() {
        var wizard = new Wizard { CharId = 7, Account = new Account { GiftMailbox = [] } };

        Assert.Null(GiftingService.NewMailNotice(wizard));
    }

}
