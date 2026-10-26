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
 * INTERACT SERVICE MEMENTO
 * ========================================================================
 * 
 * PURPOSE:
 * Manages complex NPC interaction mechanics, tracking player proximity 
 * and service component interactions for zone entities.
 * 
 * USAGE EXAMPLE:
 * Always attached to an entity. Implement a service component to add functionality.
 * 
 * NOTE:
 * Keeps track of any components of type `IServiceComponent` attached to the entity.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Interface for service components that can be attached to a zone entity.
/// </summary>
public interface IServiceComponent {

    IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter);
    void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex);
    void ForgetPlayerState(Wizard playerCharacter) { }

    string ServiceName { get; }
    string NpcIcon { get; }
    string NpcNameKey { get; }
    string NpcTextKey { get; }
    WizBangs WizBang { get; }
    string StateName { get; }
    string InteractWizBang { get; }
    string DisplayKey { get; }

    float DEFAULT_INTERACTION_RADIUS => 300.0f;

}

internal sealed class InteractServiceMementoComponent(ZoneEntity entity) 
    : ZoneEntityComponent(entity), IComponentFactory, IWithTimers {

    private const string DEFAULT_NAME_KEY = "NPCFormats_Name";
    private const string DEFAULT_TEXT_KEY = "GUI_NPCInteractText";
    private const string WIZBANG_UPDATE_TIMER_KEY = "WIZBANG_UPDATE_TIMER";
    private const double WIZBANG_UPDATE_INTERVAL_SECONDS = 1.0;
    private const int WIZARD_QUERY_TIMEOUT_SECONDS = 5;
    private const float DEFAULT_RENDER_DISTANCE = 5000.0f; // Default wizbang render distance.

    private readonly float _interactionRadius = 300.0f;
    private readonly Dictionary<ulong, IActorRef> _playersInInteractionRange = [];
    private readonly Dictionary<ulong, IActorRef> _playersInRenderRange = [];
    private List<IServiceComponent> _serviceComponents = [];
    private readonly ServiceOptionTable _optionTable = new();
    private MadlibBlock _madlibBlock;
    private float _renderDistance;

    public ITimerScheduler Timers { get; set; }

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => true;

    public override void OnStart() {
        // Set render distance - use zone far clip if available, otherwise default.
        _renderDistance = Entity.Zone?.ZoneData?.m_farClip ?? DEFAULT_RENDER_DISTANCE;
        
        _serviceComponents = [.. Entity.GetComponentsOfType<IServiceComponent>()];

        // The component set is fixed once loaded, so an entity without services never has a wizbang.
        if (_serviceComponents.Count <= 0) {
            return;
        }

        var updateInterval = TimeSpan.FromSeconds(WIZBANG_UPDATE_INTERVAL_SECONDS);
        var updateMsg = new ZONE_102_PROTOCOL.MSG_WIZBANGUPDATEINTERVAL();
        Timers.StartPeriodicTimer(WIZBANG_UPDATE_TIMER_KEY, updateMsg, updateInterval);
    }

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        DropPlayer(playerActor);
        _optionTable.Remove(id);
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_serviceComponents.Count <= 0) {
            return;
        }

        var playerId = playerObj.m_globalID.Full;
        var interactionRadius = InteractionRadius;

        // The same character can come back with a new session actor; never keep the stale one.
        if (_playersInInteractionRange.TryGetValue(playerId, out var knownActor) && knownActor != playerActor) {
            _playersInInteractionRange[playerId] = playerActor;
        }

        if (_playersInRenderRange.TryGetValue(playerId, out var knownRenderActor) && knownRenderActor != playerActor) {
            _playersInRenderRange[playerId] = playerActor;
        }

        // Handle interaction range (for service options).
        if (IsInHorizontalRadius(playerObj, interactionRadius)
            && !_playersInInteractionRange.ContainsKey(playerId)) {
            _playersInInteractionRange.Add(playerId, playerActor);
            SendActorServiceOptions(playerActor, playerId);
        }
        else if (!IsInHorizontalRadius(playerObj, interactionRadius)
                 && _playersInInteractionRange.ContainsKey(playerId)) {
            _playersInInteractionRange.Remove(playerId);
            SendLeaveServiceRange(playerActor);
        }

        // Handle render range (for wizbangs).
        if (IsInRadius(playerObj, _renderDistance)) {
            if (!_playersInRenderRange.ContainsKey(playerId)) {
                _playersInRenderRange.Add(playerId, playerActor);
            }
        }
        else if (_playersInRenderRange.ContainsKey(playerId)) {
            _playersInRenderRange.Remove(playerId);
        }
    }

    /// <summary>
    /// The smallest interaction radius any attached service asks for (default 300).
    /// </summary>
    private float InteractionRadius => _serviceComponents.Count > 0
        ? _serviceComponents.Min(c => c.DEFAULT_INTERACTION_RADIUS)
        : _interactionRadius;

    internal bool IsInInteractionRange(CoreObject playerObject)
        => IsInHorizontalRadius(playerObject, InteractionRadius);

    internal ServiceOptionBase FindSentOption(ulong playerId, IServiceComponent owner, uint localIndex)
        => _optionTable.TryGetSentOption(playerId, owner, localIndex, out var option) ? option : null;

    internal bool TryFindServiceIndex(ulong playerId, IServiceComponent owner, out uint serviceIndex)
        => _optionTable.TryFindServiceIndex(playerId, owner, out serviceIndex);

    internal void ResendServiceOptions(IActorRef playerActor, Wizard wizard, ulong playerId) {
        SendLeaveServiceRange(playerActor);
        SendServiceOptions(playerActor, wizard, playerId, reinteract: 2);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEINTERACTION))]
    private void PlayerInteraction(ZONE_102_PROTOCOL.MSG_ZONEINTERACTION message) {
        var playerActor = message.PlayerActor;
        var playerCharacter = message.PlayerCharacter;
        var playerObject = message.PlayerObject;
        var serviceName = message.ServiceName;
        var serviceIndex = message.ServiceIndex;
        var reinteract = message.Reinteract;

        if (playerObject is null) {
            Logger.Debug("Ignoring interaction from {Player} with NPC {Npc}: player position unknown",
                Logger.Args(playerActor.Path.Name, Entity.ActiveGameObject.m_debugName));

            return;
        }

        var playerId = playerObject.m_globalID.Full;

        Logger.Debug("Player {0} interacted with NPC {1} using service {2} at index {3} (Reinteract: {4})",
            Logger.Args(playerActor.Path.Name, Entity.ActiveGameObject.m_globalID.Full, serviceName, serviceIndex, reinteract));

        if (!IsInInteractionRange(playerObject)) {
            Logger.Debug("Ignoring interaction from {Player} with NPC {Npc}: out of interaction range",
                Logger.Args(playerActor.Path.Name, Entity.ActiveGameObject.m_debugName));
            _optionTable.Remove(playerId);

            return;
        }

        if (_serviceComponents.Count <= 0) {
            Logger.Warning("No service components found for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_debugName));

            return;
        }

        // If this is a reinteract scenario, send service range messages first.
        if (reinteract == 2) {
            SendLeaveServiceRange(playerActor);
            SendActorServiceOptions(playerActor, playerId, reinteract);
        }

        // Route to the component that owns this option index.
        // Using FirstOrDefault by ServiceName is unsafe when multiple components share
        // the same service name (e.g. InteractQuestSelectComponent shadowing WoodenChestComponent).
        if (!_optionTable.TryRoute(playerId, serviceIndex, out var serviceComponent, out var localIndex)) {
            Logger.Warning("No component owns service index {0} for NPC {1} with service name {2}",
                Logger.Args(serviceIndex, Entity.ActiveGameObject.m_debugName, serviceName));

            return;
        }

        serviceComponent.OnServiceInteraction(playerActor, playerCharacter, playerObject, localIndex);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUESTSTATECHANGED))]
    private void ReceiveQuestStateChanged(ZONE_102_PROTOCOL.MSG_QUESTSTATECHANGED message) {
        if (!string.IsNullOrEmpty(message.PersonaName) && !IsPersona(message.PersonaName)) {
            return;
        }

        if (!_playersInInteractionRange.TryGetValue(message.PlayerGlobalId, out var playerActor)) {
            return;
        }

        SendLeaveServiceRange(playerActor);
        SendActorServiceOptions(playerActor, message.PlayerGlobalId);
    }

    private bool IsPersona(string personaName)
        => Entity.Template is GameObjectTemplate template
           && PersonaName.Normalize(template.m_objectName) == PersonaName.Normalize(personaName);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_WIZBANGUPDATEINTERVAL))]
    private void HandleWizBangUpdateInterval(ZONE_102_PROTOCOL.MSG_WIZBANGUPDATEINTERVAL message) {
        if (_serviceComponents.Count <= 0 || _playersInRenderRange.Count == 0) {
            return;
        }

        // Query every nearby player's wizard concurrently instead of
        // blocking on each one sequentially.
        var playerActors = _playersInRenderRange.Values.ToArray();
        var queryTasks = playerActors.Select(async playerActor => {
            try {
                var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
                var rsp = await playerActor.Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg, TimeSpan.FromSeconds(WIZARD_QUERY_TIMEOUT_SECONDS));
                return rsp.Wizard;
            }
            catch {
                return null;
            }
        });

        Task.WhenAll(queryTasks)
            .ContinueWith(t => {
                var wizards = t.Result.ToArray();
                return new ZONE_102_PROTOCOL.MSG_WIZBANG_UPDATE_RESULT {
                    PlayerActors = playerActors,
                    Wizards = wizards
                };
            })
            .PipeTo(Self);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_WIZBANG_UPDATE_RESULT))]
    private void HandleWizBangUpdateResult(ZONE_102_PROTOCOL.MSG_WIZBANG_UPDATE_RESULT result) {
        for (int i = 0; i < result.PlayerActors.Length; i++) {
            var wizard = result.Wizards[i];
            if (wizard == null) {
                // Timed out or failed: the player is gone or unresponsive.
                DropPlayer(result.PlayerActors[i]);

                continue;
            }

            SendWizBang(result.PlayerActors[i], wizard);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SERVICEOPTIONS_RESULT))]
    private void HandleServiceOptionsResult(ZONE_102_PROTOCOL.MSG_SERVICEOPTIONS_RESULT result) {
        if (result.Wizard == null) {
            DropPlayer(result.PlayerActor);

            return;
        }

        SendServiceOptions(result.PlayerActor, result.Wizard, result.PlayerId, result.Reinteract);
    }

    private void DropPlayer(IActorRef playerActor) {
        foreach (var key in _playersInInteractionRange.Where(x => x.Value == playerActor).Select(x => x.Key).ToArray()) {
            _playersInInteractionRange.Remove(key);
        }

        foreach (var key in _playersInRenderRange.Where(x => x.Value == playerActor).Select(x => x.Key).ToArray()) {
            _playersInRenderRange.Remove(key);
        }
    }

    private void SendActorServiceOptions(IActorRef playerActor, ulong playerId, int reinteract = 0) {
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        playerActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg, TimeSpan.FromSeconds(WIZARD_QUERY_TIMEOUT_SECONDS))
            .ContinueWith(t => new ZONE_102_PROTOCOL.MSG_SERVICEOPTIONS_RESULT {
                PlayerActor = playerActor,
                PlayerId = playerId,
                Wizard = t.IsCompletedSuccessfully ? t.Result.Wizard : null,
                Reinteract = reinteract
            })
            .PipeTo(Self);
    }

    private void SendServiceOptions(IActorRef playerActor, Wizard wizard, ulong playerId, int reinteract) {
        var serviceMemento = BuildServiceMemento(wizard, playerId);

        // If we have no service options, do not send anything.
        if (serviceMemento is null || serviceMemento.m_serviceOptions.Count <= 0) {
            return;
        }

        // Serialize the service memento and send it to the player.
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(serviceMemento, 4, out var data)) {
            Logger.Error("Failed to serialize service memento for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_debugName));

            return;
        }

        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            // Do not let this property name fool you. 
            // The client incorrectly labels this property as "MobileID." It is in fact
            // the global ID of the NPC. It will not work if you set it to the mobile ID.
            MobileID = Entity.ActiveGameObject.m_globalID.Full,
            Options = data,
            Reinteract = reinteract
        };

        playerActor.Tell(npcOptionsMsg);
    }

    private void SendLeaveServiceRange(IActorRef playerActor) {
        var msg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            // Do not let this property name fool you. 
            // The client incorrectly labels this property as "MobileID." It is in fact
            // the global ID of the NPC. It will not work if you set it to the mobile ID.
            MobileID = Entity.ActiveGameObject.m_globalID.Full
        };

        playerActor.Tell(msg);
    }

    private ServiceMementoBase BuildServiceMemento(Wizard playerCharacter, ulong playerId) {
        _serviceComponents = [.. Entity.GetComponentsOfType<IServiceComponent>()];
        if (_serviceComponents.Count <= 0) {
            return null;
        }

        var gameObjTemplate = Entity.Template as GameObjectTemplate;

        var componentOptions = CollectFreshOptions(_serviceComponents, playerCharacter);
        var allOptions = _optionTable.Build(playerId, componentOptions);
        var componentsWithOptions = componentOptions
            .Where(x => x.Options.Count > 0)
            .Select(x => x.Component)
            .ToList();

        // Get UI overrides based on priority.
        // A component with nothing to offer must not set the dialog's icon, name and text (a quest
        // interactable that is currently unused would otherwise shadow an "Enter door" prompt).
        var sortedComponents = SortComponentsByPriority(componentsWithOptions.Count > 0 ? componentsWithOptions : _serviceComponents);
        var highestPriority = sortedComponents.FirstOrDefault();

        SetMadLibBlock();

        var npcIconOrDefault = string.IsNullOrEmpty(highestPriority?.NpcIcon)
            ? gameObjTemplate?.m_sIcon.ToString()
            : highestPriority.NpcIcon;
        var npcNameKeyOrDefault = string.IsNullOrEmpty(highestPriority?.NpcNameKey)
            ? DEFAULT_NAME_KEY
            : highestPriority.NpcNameKey;
        var npcTextKeyOrDefault = string.IsNullOrEmpty(highestPriority?.NpcTextKey)
            ? DEFAULT_TEXT_KEY
            : highestPriority.NpcTextKey;

        return new ServiceMementoBase {
            m_npcIcon = npcIconOrDefault ?? string.Empty,
            m_npcNameKey = npcNameKeyOrDefault ?? DEFAULT_NAME_KEY,
            m_npcTextKey = npcTextKeyOrDefault ?? DEFAULT_TEXT_KEY,
            m_serviceOptions = allOptions,
            m_personaMadlibs = _madlibBlock
        };
    }

    internal static List<(IServiceComponent Component, List<ServiceOptionBase> Options)> CollectFreshOptions(
        IReadOnlyList<IServiceComponent> components, Wizard playerCharacter) {
        // Every component first, since one component's options can read another's state (a pending persona goal hides offers).
        foreach (var component in components) {
            component.ForgetPlayerState(playerCharacter);
        }

        return [.. components.Select(component => (component, component.GetServiceOptions(playerCharacter).ToList()))];
    }

    private void SetMadLibBlock() {
        // NPCs normally have a madlib of first name, last name, and title.
        // To avoid hardcoding these values, we use the entity's madlib name.
        // We'll also set the madlib token to just "NAME" so the client displays the name as-is.
        var madlibName = Entity.GetMadlibName();
        if (madlibName is null) {
            return;
        }

        var madlibList = new List<MadlibArg> {
            new MadlibArgT_ByteString() {
                m_madlibArgument = madlibName,
                m_madlibToken = "NAME"
            },
        };

        _madlibBlock = new MadlibBlock() {
            m_blockToken = "NPC",
            m_madlibs = madlibList
        };
    }

    private void SendWizBang(IActorRef playerActor, Wizard playerWizard) {
        if (_serviceComponents.Count <= 0) {
            return;
        }

        // Collect wizbangs from components that have service options for this player.
        var activeWizBangs = new List<WizBangs>();
        foreach (var component in _serviceComponents) {
            var serviceOptions = component.GetServiceOptions(playerWizard);
            if (serviceOptions.Any()) {
                activeWizBangs.Add(component.WizBang);
            }
        }

        // Get highest priority wizbang for this player, or None to clear.
        var wizBang = WizBangPriority.GetHighestPriorityWizBang(activeWizBangs);

        // Re-send every tick rather than once per change: a single send can be lost
        // while the client is still building the scene, and retail re-pushes each update.
        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = (uint) wizBang,
            GameObjectID = Entity.ActiveGameObject.m_globalID.Full
        };
        playerActor.Tell(wizBangMsg);
    }

    private static IOrderedEnumerable<IServiceComponent> SortComponentsByPriority(IEnumerable<IServiceComponent> components) {
        // Sort by WizBang priority
        var wizBangs = components.Select(c => c.WizBang);
        var prioritySortedWizBangs = WizBangPriority.GetPrioritySortedWizBangs([.. wizBangs]);

        // If priority sorted WizBangs are empty, sort by default.
        if (prioritySortedWizBangs is null || prioritySortedWizBangs.Count <= 0) {
            return components.OrderBy(c => c.WizBang);
        }

        return components.OrderBy(c => prioritySortedWizBangs.IndexOf(c.WizBang));
    }

}