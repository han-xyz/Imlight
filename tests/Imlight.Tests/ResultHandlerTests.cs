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
 * Last Updated: 10/25/2026
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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class ResultHandlerTests : IDisposable {

    private const uint WoodChestOpenedTemplateId = 1374674914;
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

        Assert.InRange(loot.GoldAmount, 50, 140);
        var item = Assert.Single(loot.Items);
        Assert.Equal(("77825", 2), (item.ItemId, item.Quantity));
    }

    [Fact]
    public void ResLoot_WithoutLootTable_GrantsChestGold() {
        var loot = ResLootHandler.LootFor(null);

        Assert.InRange(loot.GoldAmount, 10, 100);
        Assert.Empty(loot.Items);
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
