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
 * RESULT HANDLER TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the result handlers a generic chest's interact
 * result list runs: loot, the activity-tracking result and the open sound.
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
using System.Collections.Generic;
using System.IO;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.Game.Results.Handlers;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class ResultHandlerTests : IDisposable {

    private const uint WoodChestOpenedTemplateId = 1374674914;
    private const uint AddExpansionHash = 0x5820E493;
    private const string ChestName = "Chest_01";
    private const ulong CharacterId = 1001;
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    private readonly ActorSystem _system = ActorSystem.Create("result-handler-tests");
    private readonly Inbox _player;

    public ResultHandlerTests() {
        InitializeQuietLogging();
        _player = Inbox.Create(_system);
    }

    public void Dispose() => _system.Terminate().Wait(s_timeout);

    [Fact]
    public void ResLoot_GrantsAndSendsLoot() {
        var lootTable = new LootInfoList {
            m_goldInfo = new GoldLootInfo { m_lootType = LOOT_TYPE.LOOT_TYPE_GOLD, m_goldAmount = 40 },
            m_loot = [new ItemLootInfo { m_lootType = LOOT_TYPE.LOOT_TYPE_ITEM, m_itemID = new GID(77825), m_numItems = 2 }],
        };

        var loot = ResLootHandler.LootFor(lootTable);

        Assert.Equal(40, loot.GoldAmount);
        var item = Assert.Single(loot.Items);
        Assert.Equal(("77825", 2), (item.ItemId, item.Quantity));
    }

    [Fact]
    public void ResLoot_WithoutLootTable_GrantsChestGold() {
        var loot = ResLootHandler.ChestGold();

        Assert.InRange(loot.GoldAmount, 10, 100);
        Assert.Empty(loot.Items);
    }

    [Fact]
    public void ResLoot_WithoutLootTable_FromNamelessSource_SendsNothing() {
        var player = StubPlayer(new Wizard { CharId = CharacterId });
        var result = new ResLoot();

        Assert.True(Execute<ResLootHandler>(result, new PlayerContext(player, result)));
        Assert.Throws<TimeoutException>(() => _player.Receive(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void ResLoot_WithoutWizard_Fails() {
        var player = StubPlayer(wizard: null);

        Assert.False(Execute<ResLootHandler>(new ResLoot(), new PlayerContext(player, new ResLoot())));
    }

    [Fact]
    public void ResLoot_SecondInteraction_SameCharacter_NoGrant() {
        var zoneState = new ZoneScriptState();
        zoneState.TryClaimLoot(ChestName, CharacterId);
        var player = StubPlayer(new Wizard { CharId = CharacterId });
        var result = new ResLoot();

        var success = Execute<ResLootHandler>(result, new PlayerContext(player, result, ChestName, zoneState));

        Assert.True(success);
        Assert.Throws<TimeoutException>(() => _player.Receive(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void ResLoot_OtherCharacter_StillGrants() {
        var zoneState = new ZoneScriptState();

        Assert.True(zoneState.TryClaimLoot(ChestName, CharacterId));
        Assert.True(zoneState.TryClaimLoot(ChestName, CharacterId + 1));
    }

    [Fact]
    public void ResUnknown77D46519_ReturnsTrue() {
        Assert.True(Execute<ResUnknown77D46519Handler>(new ResUnknown77D46519 { m_activityType = 25 }));
    }

    [Fact]
    public void AddExpansionHash_RegistersAResult() {
        var type = new ServerGeneratedTypeRegistry().LookupType(AddExpansionHash);

        Assert.True(typeof(Result).IsAssignableFrom(type));
    }

    [Fact]
    public void ResPlaySound_KnownName_ResolvesTemplate() {
        var success = Execute<ResPlaySoundHandler>(new ResPlaySound { m_soundName = "Wood Chest Opened" });

        Assert.True(success);
        var playSound = Assert.IsType<GAME_5_PROTOCOL.MSG_PLAYSOUND>(_player.Receive(s_timeout));
        Assert.Equal(WoodChestOpenedTemplateId, playSound.SoundID);
    }

    [Fact]
    public void ResPlaySound_UnknownName_WarnsAndSucceeds() {
        var success = Execute<ResPlaySoundHandler>(new ResPlaySound { m_soundName = "No Such Sound" });

        Assert.True(success);
        Assert.Throws<TimeoutException>(() => _player.Receive(TimeSpan.FromMilliseconds(200)));
    }

    public static TheoryData<Result> UnhandledQuestResults => [
        new ResWizPlayMusic(),
        new ResDownloadPackage(),
        new ResDownloadElement(),
        new ResMarkZoneNoWarn(),
        new ResTimeStampEntry(),
        new ResAddMissionDoor(),
        new ResRemoveMissionDoor(),
        new ResCinematic(),
        new ResAddBadge(),
        new ResAddTrainingPoints(),
        new ResAddTreasureSpell(),
        new ResAddEncounterXP(),
        new ResReInteract(),
        new ResSetGardeningLevel(),
        new ResAddExpansion(),
    ];

    [Theory]
    [MemberData(nameof(UnhandledQuestResults))]
    public void UnhandledQuestResults_AllRegistered(Result result) {
        var handler = ResultDispatcher.FindHandlerForResult(result.GetType(), new PlayerContext(_player.Receiver, result));

        Assert.NotNull(handler);
    }

    [Fact]
    public void ResWizPlayMusic_ForwardsToClient() {
        var success = Execute<ResWizPlayMusicHandler>(new ResWizPlayMusic {
            m_musicName = "WizardCity4M",
            m_fadeTime = 3f,
            m_startDelay = 1.5f,
        });

        Assert.True(success);
        var playMusic = Assert.IsType<WIZARD_12_PROTOCOL.MSG_PLAYMUSIC>(_player.Receive(s_timeout));
        Assert.Equal(("WizardCity4M", 3f, 1.5f), (playMusic.SoundFilename.ToString(), playMusic.FadeTime, playMusic.StartDelay));
    }

    [Fact]
    public void ResAddBadge_WithoutBadgeStorage_Succeeds() {
        Assert.True(Execute<ResAddBadgeHandler>(new ResAddBadge { m_badgeName = "FinishG14BarkinghamPalace" }));
    }

    [Fact]
    public void ResAddTrainingPoints_AddsOnePoint() {
        Assert.Equal(4, ResAddTrainingPointsHandler.TrainingPointsAfterGrant(3));
    }

    [Fact]
    public void ResSetGardeningLevel_UnlocksGardening() {
        var wizard = new Wizard { GameStats = new ServerWizGameStats(MagicSchool.Fire, 1) };
        var result = new ResSetGardeningLevel();

        Assert.True(Execute<ResSetGardeningLevelHandler>(result, new PlayerContext(StubPlayer(wizard), result)));
        Assert.Equal(1, wizard.GameStats.m_gardeningLevel);
    }

    [Fact]
    public void ResSetGardeningLevel_KeepsHigherLevel() {
        var wizard = new Wizard { GameStats = new ServerWizGameStats(MagicSchool.Fire, 1) { m_gardeningLevel = 5 } };
        var result = new ResSetGardeningLevel();

        Assert.True(Execute<ResSetGardeningLevelHandler>(result, new PlayerContext(StubPlayer(wizard), result)));
        Assert.Equal(5, wizard.GameStats.m_gardeningLevel);
    }

    [Fact]
    public void ResReInteract_AsksSessionToRefreshThePersona() {
        var result = new ResReInteract { m_personaName = "GH2-HFJORD-NPC15_Persona" };

        Assert.True(Execute<ResReInteractHandler>(result));

        var request = Assert.IsType<CHARACTER_103_PROTOCOL.MSG_REQUESTNPCREFRESH>(_player.Receive(s_timeout));
        Assert.Equal("GH2-HFJORD-NPC15_Persona", request.PersonaName);
    }

    [Fact]
    public void ResActorDialog_ReportsTheDialogToTheSession() {
        Assert.True(Execute<ResActorDialogHandler>(new ResActorDialog()));

        Assert.IsType<WIZARD_12_PROTOCOL.MSG_ACTORDIALOG>(_player.Receive(s_timeout));
        Assert.IsType<CHARACTER_103_PROTOCOL.MSG_DIALOGSHOWN>(_player.Receive(s_timeout));
    }

    private bool Execute<THandler>(Result result) where THandler : ActorBase
        => Execute<THandler>(result, new PlayerContext(_player.Receiver, result));

    private bool Execute<THandler>(Result result, IResultContext context) where THandler : ActorBase {
        var handler = _system.ActorOf(Props.Create<THandler>());
        handler.Tell(new CHARACTER_103_PROTOCOL.MSG_INITIALIZEHANDLER {
            Context = context,
            Result = result,
        });

        return handler.Ask<CHARACTER_103_PROTOCOL.MSG_RESULTEXECUTED>(
            new CHARACTER_103_PROTOCOL.MSG_EXECUTEHANDLER(), s_timeout).Result.Success;
    }

    private static void InitializeQuietLogging() {
        var iniFilePath = Path.GetTempFileName();
        var logPath = Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt");
        try {
            File.WriteAllText(iniFilePath, $"[Logging]\nLogPath = {logPath}\nLogLevel = ERROR\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    private IActorRef StubPlayer(Wizard? wizard)
        => _system.ActorOf(Props.Create(() => new StubPlayerActor(_player.Receiver, wizard)));

    private sealed class StubPlayerActor : ReceiveActor {

        public StubPlayerActor(IActorRef inbox, Wizard? wizard) {
            Receive<CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD>(
                _ => Sender.Tell(new CHARACTER_103_PROTOCOL.MSG_CHARACTER { Wizard = wizard! }));
            ReceiveAny(message => inbox.Tell(message));
        }

    }

    private sealed class PlayerContext(IActorRef player, Result result, string? sourceName = null,
        ZoneScriptState? zoneState = null) : IResultContext {

        public IEnumerable<Result> GetResults() => [result];
        public IActorRef GetZoneActor() => ActorRefs.Nobody;
        public IActorRef GetPlayerRef() => player;
        public CoreObject GetPlayerObj() => null!;
        public IActorRef GetReplyTo() => ActorRefs.Nobody;
        public string GetSourceName() => sourceName!;
        public ZoneScriptState GetZoneState() => zoneState!;

    }

}
