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
 * HIDDEN QUESTS TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the quest log's hidden quests blob: the size cap, and the blob reaching the player object on attach.
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
 * Last Updated: 10/26/2026
 */

using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

public sealed class HiddenQuestsTests {

    private static WizClientObject AttachedObject(Wizard wizard) {
        var clientObject = new WizClientObject { m_inactiveBehaviors = [new HiddenQuestsBehavior()] };
        WizardObjectLoader.SetHiddenQuestsBehavior(clientObject, wizard);
        return clientObject;
    }

    private static ByteString HiddenQuestsDataOf(WizClientObject clientObject)
        => ((HiddenQuestsBehavior) clientObject.m_inactiveBehaviors[0]).m_hiddenQuestsData;

    [Fact]
    public void BlobAtTheCapIsStored() {
        var wizard = new Wizard { CharId = 1 };
        var blob = new ByteString(new byte[HiddenQuestsService.MaxHiddenQuestsDataBytes]);

        Assert.True(HiddenQuestsService.TryStoreHiddenQuests(wizard, blob));
        Assert.Equal(blob, wizard.HiddenQuestsData);
    }

    [Fact]
    public void BlobOverTheCapIsRejectedAndTheStoredOneKept() {
        var wizard = new Wizard { CharId = 1, HiddenQuestsData = "kept" };
        var blob = new ByteString(new byte[HiddenQuestsService.MaxHiddenQuestsDataBytes + 1]);

        Assert.False(HiddenQuestsService.TryStoreHiddenQuests(wizard, blob));
        Assert.Equal("kept", (string) wizard.HiddenQuestsData);
    }

    [Fact]
    public void StoredBlobReachesTheBehaviorOnAttach() {
        var wizard = new Wizard { CharId = 1 };
        HiddenQuestsService.TryStoreHiddenQuests(wizard, "quest-a;quest-b");

        Assert.Equal("quest-a;quest-b", (string) HiddenQuestsDataOf(AttachedObject(wizard)));
    }

    [Fact]
    public void EmptyBlobClearsTheHiddenSet() {
        var wizard = new Wizard { CharId = 1, HiddenQuestsData = "quest-a" };

        Assert.True(HiddenQuestsService.TryStoreHiddenQuests(wizard, ByteString.Empty));
        Assert.True(HiddenQuestsDataOf(AttachedObject(wizard)).IsEmpty);
    }

}
