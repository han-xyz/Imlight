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

using System;
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

    private static readonly ByteString WithOneEntry = new(new byte[] {
        0xE7, 0xAC, 0xCE, 0xFA, 0x01, 0x01, 0x00, 0x00, 0x00, 0x11, 0x22, 0x33, 0x44, 0x00, 0x00 });

    [Fact]
    public void BlobAtTheCapIsStored() {
        var wizard = new Wizard { CharId = 1 };
        var entries = (HiddenQuestsService.MaxHiddenQuestsDataBytes - 10) / 5;
        var bytes = new byte[10 + entries * 5];
        ((byte[]) HiddenQuestsBlob.Empty).CopyTo(bytes, 0);
        BitConverter.GetBytes(entries).CopyTo(bytes, 5);
        var blob = new ByteString(bytes);

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
        HiddenQuestsService.TryStoreHiddenQuests(wizard, WithOneEntry);

        Assert.Equal(WithOneEntry, HiddenQuestsDataOf(AttachedObject(wizard)));
    }

    [Fact]
    public void EmptyBlobIsWellFormed() {
        Assert.True(HiddenQuestsBlob.IsWellFormed(((byte[]) HiddenQuestsBlob.Empty)));
        Assert.Equal(10, HiddenQuestsBlob.Empty.Length);
    }

    [Fact]
    public void BlobWithEntriesIsWellFormed() {
        Assert.True(HiddenQuestsBlob.IsWellFormed(((byte[]) WithOneEntry)));
    }

    [Fact]
    public void MalformedBlobsAreNotWellFormed() {
        var good = ((byte[]) WithOneEntry);
        var badMagic = (byte[]) good.Clone();
        badMagic[0] = 0;
        var badVersion = (byte[]) good.Clone();
        badVersion[4] = 2;
        var badCount = (byte[]) good.Clone();
        badCount[5] = 2;

        Assert.False(HiddenQuestsBlob.IsWellFormed([]));
        Assert.False(HiddenQuestsBlob.IsWellFormed(good[..^1]));
        Assert.False(HiddenQuestsBlob.IsWellFormed(badMagic));
        Assert.False(HiddenQuestsBlob.IsWellFormed(badVersion));
        Assert.False(HiddenQuestsBlob.IsWellFormed(badCount));
    }

    [Fact]
    public void MalformedBlobIsRejectedAndTheStoredOneKept() {
        var wizard = new Wizard { CharId = 1, HiddenQuestsData = WithOneEntry };

        Assert.False(HiddenQuestsService.TryStoreHiddenQuests(wizard, "quest-a"));
        Assert.Equal(WithOneEntry, wizard.HiddenQuestsData);
    }

    [Fact]
    public void EmptyBlobClearsTheHiddenSet() {
        var wizard = new Wizard { CharId = 1, HiddenQuestsData = WithOneEntry };

        Assert.True(HiddenQuestsService.TryStoreHiddenQuests(wizard, HiddenQuestsBlob.Empty));
        Assert.Equal(HiddenQuestsBlob.Empty, HiddenQuestsDataOf(AttachedObject(wizard)));
    }

    [Fact]
    public void CharacterWithNoStoredBlobGetsTheEmptyBlobOnAttach() {
        Assert.Equal(HiddenQuestsBlob.Empty, HiddenQuestsDataOf(AttachedObject(new Wizard { CharId = 1 })));
    }

    [Fact]
    public void ObjectSentToOtherPlayersCarriesTheEmptyBlob() {
        var wizard = new Wizard { CharId = 1 };
        HiddenQuestsService.TryStoreHiddenQuests(wizard, WithOneEntry);

        var clientObject = AttachedObject(wizard);
        WizardObjectLoader.RemoveOwnerOnlyData(clientObject);

        Assert.Equal(HiddenQuestsBlob.Empty, HiddenQuestsDataOf(clientObject));
    }

}
