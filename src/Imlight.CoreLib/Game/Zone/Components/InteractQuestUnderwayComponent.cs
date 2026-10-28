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
 * INTERACT QUEST UNDERWAY COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages quest underway interactions for NPCs when players have active quests from them.
 * Shows the grey '?' wizbang when a player has a quest in progress from this NPC.
 * 
 * USAGE EXAMPLE:
 * Automatically attached to NPCs that give quests based on quest dialog configuration.
 * 
 * NOTE:
 * Only attaches to NPCs that have "Prep" dialogs in quest templates.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/28/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal class PlayerQuestUnderwayState {

    public List<QuestTemplate> UnderwayQuests { get; set; } = [];
    public DateTime LastUpdated { get; set; }

}

internal sealed class InteractQuestUnderwayComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string PREP_NPC_ICON = "Prep";
    private const double WIZBANG_UPDATE_INTERVAL_SECONDS = 1.0;

    public string ServiceName => "QuestUnderwayService";
    public bool FollowsQuestState => true;
    public string NpcIcon => "";
    public string NpcNameKey => null;
    public string NpcTextKey => null;
    public string StateName => "";
    public string InteractWizBang => "";
    public string DisplayKey => "";

    private WizBangs _wizBang = WizBangs.None;
    public WizBangs WizBang {
        get => _wizBang;
        private set {
            _wizBang = value;
        }
    }

    private readonly List<QuestTemplate> _givesQuests = [];
    private readonly Dictionary<ulong, DateTime> _lastWizBangUpdate = [];
    private readonly Dictionary<ulong, PlayerQuestUnderwayState> _cachedPlayerStates = [];

    public static bool ShouldAttachToEntity(CoreTemplate template) {
        if (template is not GameObjectTemplate goTemplate ||
            !template.m_behaviors.Any(x => x is NPCBehaviorTemplate) ||
            template.m_behaviors.Any(x => x is DuelistBehaviorTemplate)) {
            return false;
        }

        // Check if this NPC gives any quests by looking for "Prep" dialogs.
        return QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x?.m_dialogList is ActorDialogList)
            .Any(quest => {
                var dialogList = quest.m_dialogList as ActorDialogList;
                var prepDialogEntry = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");

                return DialogSpeakers.IsSpokenBy(prepDialogEntry?.m_dialogEntries?.FirstOrDefault(), goTemplate);
            });
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard wizard) {
        if (wizard is null) {
            yield break;
        }

        // Persona goals take priority over the underway entry.
        if (InteractPersonaGoalComponent.HasPendingGoalFor(Entity, wizard)) {
            yield break;
        }

        var state = GetOrUpdatePlayerState(wizard);

        // The memento wizbang tick reads this property; refresh it from live state.
        WizBang = state.UnderwayQuests.Count > 0 ? WizBangs.UnfinishedQuest : WizBangs.None;

        foreach (var quest in state.UnderwayQuests) {
            yield return ToUnderwayOption(quest);
        }
    }

    internal PrepEntry ToUnderwayOption(QuestTemplate quest)
        => new() {
            m_displayKey = quest.m_questTitle,
            m_iconKey = PREP_NPC_ICON,
            m_serviceName = ServiceName,
        };

    internal static List<QuestTemplate> FindUnderwayQuests(IEnumerable<QuestTemplate> givesQuests, Wizard wizard)
        => [.. givesQuests
            .Where(quest => wizard.HasQuest(quest.m_questName) && !wizard.HasCompletedQuest(quest.m_questName))
            .OrderBy(quest => (string) quest.m_questName, StringComparer.Ordinal)];

    public override void OnStart() {
        // Cache the quests this NPC can give during startup.
        var questTemplates = QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x is not null)
            .ToList();

        foreach (var questTemplate in questTemplates) {
            if (questTemplate.m_dialogList is not ActorDialogList dialogList) {
                continue;
            }

            var prepDialogEntry = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");

            if (DialogSpeakers.IsSpokenBy(prepDialogEntry?.m_dialogEntries?.FirstOrDefault(), Entity.Template)
                && HasUnderwayDialog(questTemplate)) {
                _givesQuests.Add(questTemplate);
            }
        }
    }

    internal static bool HasUnderwayDialog(QuestTemplate quest)
        => (quest.m_dialogList as ActorDialogList)?.m_dialogs?
            .FirstOrDefault(de => de.m_dialogTag == "Underway")?.m_dialogEntries is { Count: > 0 };

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_givesQuests.Count <= 0) {
            WizBang = WizBangs.None;

            return;
        }

        var state = GetOrUpdatePlayerState(playerWizard, forceUpdate: true);
        WizBang = state.UnderwayQuests.Count > 0 ? WizBangs.UnfinishedQuest : WizBangs.None;
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (playerWizard is null || _givesQuests.Count <= 0) {
            return;
        }

        var now = DateTime.UtcNow;
        var playerId = playerWizard.CharId;

        if (_lastWizBangUpdate.TryGetValue(playerId, out var lastUpdate)) {
            if ((now - lastUpdate).TotalSeconds < WIZBANG_UPDATE_INTERVAL_SECONDS) {
                return;
            }
        }

        _lastWizBangUpdate[playerId] = now;
        var state = GetOrUpdatePlayerState(playerWizard, forceUpdate: true);
        WizBang = state.UnderwayQuests.Count > 0 ? WizBangs.UnfinishedQuest : WizBangs.None;
    }

    public void ForgetPlayerState(Wizard playerCharacter)
        => _cachedPlayerStates.Remove(playerCharacter.CharId);

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var memento = Entity.GetComponentOfType<InteractServiceMementoComponent>();
        var playerId = playerObject.m_globalID.Full;
        var sentOption = memento?.FindSentOption(playerId, this, serviceOptionIndex);
        var state = GetOrUpdatePlayerState(playerCharacter, forceUpdate: true);

        var quest = FindClickedQuest(sentOption, state.UnderwayQuests);
        if (quest is null) {
            Logger.Debug("Quest underway option {OptionIndex} on NPC {Npc} is no longer held; resending options",
                Logger.Args(serviceOptionIndex, Entity.ActiveGameObject.m_debugName));
            memento?.ResendServiceOptions(playerActor, playerCharacter, playerId);

            return;
        }

        ShowQuestUnderwayDialog(playerActor, quest, playerCharacter);
    }

    internal static QuestTemplate FindClickedQuest(ServiceOptionBase sentOption, IEnumerable<QuestTemplate> underwayQuests)
        => sentOption is PrepEntry entry
            ? underwayQuests.FirstOrDefault(quest => (string) quest.m_questTitle == (string) entry.m_displayKey)
            : null;

    private PlayerQuestUnderwayState GetOrUpdatePlayerState(Wizard wizard, bool forceUpdate = false) {
        var playerId = wizard.CharId;
        var now = DateTime.UtcNow;

        if (!forceUpdate && _cachedPlayerStates.TryGetValue(playerId, out var cachedState)) {
            if ((now - cachedState.LastUpdated).TotalSeconds < WIZBANG_UPDATE_INTERVAL_SECONDS) {
                return cachedState;
            }
        }

        var state = new PlayerQuestUnderwayState {
            UnderwayQuests = FindUnderwayQuests(_givesQuests, wizard),
            LastUpdated = now,
        };

        _cachedPlayerStates[playerId] = state;

        return state;
    }

    private void ShowQuestUnderwayDialog(IActorRef playerActor, QuestTemplate quest, Wizard wizard) {
        var dialogList = quest.m_dialogList as ActorDialogList;
        var underwayDialogList = dialogList?.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Underway");

        if (underwayDialogList == null) {
            Logger.Error("Quest {0} has no 'Underway' dialog entry.",
                Logger.Args(quest.m_questName));

            return;
        }

        SendActorDialog(playerActor, underwayDialogList, "QuestInfo", wizard);
    }

    private void SendActorDialog(IActorRef playerActor, ActorDialog dialogEntry, string completionType, Wizard wizard,
        ulong questId = 0, ulong goalId = 0) {
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(DialogSpeakers.ForClient(dialogEntry, wizard), 16, out var serializedData)) {
            Logger.Error("Failed to serialize '{0}' dialog.",
                Logger.Args(completionType));

            return;
        }

        var dialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = questId > 0 ? new GID(Entity.ActiveGameObject.m_nMobileID) : Entity.ActiveGameObject.m_nMobileID,
            QuestID = questId,
            GoalID = goalId,
            CompletionType = completionType,
            ActorDialog = serializedData,
            Persona = "",
            PersonaName = "",
            PersonaIcon = "",
        };

        playerActor.Tell(dialogMsg);
        playerActor.Tell(new CHARACTER_103_PROTOCOL.MSG_DIALOGSHOWN());
    }

}