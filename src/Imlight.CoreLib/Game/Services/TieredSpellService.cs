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
 * SPELLEMENT SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Answers the requests of the Spellements page: which tiered spell groups the wizard can see, upgrades paid
 * with shards, refunds, the one-time reset for multiple paths and the done-prep and marked-as-untrained lists.
 *
 * USAGE EXAMPLE:
 * The client sends MSG_CANSEETIEREDSPELLGROUPS, MSG_REQUESTTSDONEPREPFORMP and MSG_REQUESTTSMARKEDASUNTRAINED
 * when the page opens; each is answered once. The server never pushes.
 *
 * NOTE:
 * Shards, gold, the spellbook and the progress change together under the wizard's backpack lock.
 * The lists the client reads are comma-separated spell template ids.
 *
 * TODO:
 * - Which request does MSG_TIEREDSPELLRESETFORMPRESP answer? The one-time reset arrives as DOPREPFORMP.
 *
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.Types;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Game.TieredSpells;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class TieredSpellService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const int LoggedVisibilityBytes = 64;
    private const string ListSeparator = ",";

    private static readonly char[] s_listSeparators = [',', ';', ' '];

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TieredSpellService(parentActor));

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_CANSEETIEREDSPELLGROUPS))]
    private void ReceiveCanSeeTieredSpellGroups(WIZARD2_53_PROTOCOL.MSG_CANSEETIEREDSPELLGROUPS message) {
        var requestVisibility = (byte[]) message.Visibility ?? [];
        Logger.Debug("Tiered spell group visibility requested with {Length} bytes starting {Visibility}, shard type {ShardType}.",
            Logger.Args(requestVisibility.Length,
                Convert.ToHexString(requestVisibility, 0, Math.Min(requestVisibility.Length, LoggedVisibilityBytes)),
                message.ShardType));

        SendVisibility(GetActiveWizard(), message.CheckTieredSpellNotifications, message.ShardType);
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTTSDONEPREPFORMP))]
    private void ReceiveRequestDonePrepForMultiplePaths(WIZARD3_56_PROTOCOL.MSG_REQUESTTSDONEPREPFORMP _) {
        var wizard = GetActiveWizard();
        var prepped = wizard.TieredSpells.Where(progress => progress.DonePrepForMultiplePaths)
            .Select(progress => progress.TierOneSpellId);

        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_UPDATETSDONEPREPFORMP { Data = string.Join(ListSeparator, prepped) });
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTTSMARKEDASUNTRAINED))]
    private void ReceiveRequestMarkedAsUntrained(WIZARD3_56_PROTOCOL.MSG_REQUESTTSMARKEDASUNTRAINED _) {
        var wizard = GetActiveWizard();
        var untrained = wizard.TieredSpells.Where(progress => !wizard.SpellbookBehavior.HasSpell(progress.CurrentSpellId))
            .Select(progress => progress.CurrentSpellId);

        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_UPDATETSMARKEDASUNTRAINED { Data = string.Join(ListSeparator, untrained) });
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLUPGRADE))]
    private void ReceiveTieredSpellUpgrade(WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLUPGRADE message) {
        var wizard = GetActiveWizard();
        var groups = TieredSpellIndex.Instance.Groups;
        var decision = message.TieredSpellGroup < groups.Count
            ? Upgrade(wizard, (int) message.TieredSpellGroup, message.SpellID, message.SpellIDNew)
            : NotUpgraded(TieredSpellUpgradeOutcome.NotNextTier);

        Logger.Debug("Tiered spell upgrade from {SpellId} to {NewSpellId} in group {Group}: {Outcome}.",
            Logger.Args(message.SpellID, message.SpellIDNew, message.TieredSpellGroup, decision.Outcome));

        var upgraded = decision.Outcome == TieredSpellUpgradeOutcome.Upgraded;
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLUPGRADERESP {
            Success = ToSuccess(upgraded),
            FailedRequirementIndex = decision.FailedRequirementIndex,
        });

        if (upgraded) {
            SendVisibility(wizard, 0, (GID) groups[(int) message.TieredSpellGroup].ShardTemplateId);
        }
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLREFUND))]
    private void ReceiveTieredSpellRefund(WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLREFUND message) {
        var wizard = GetActiveWizard();
        var refunded = StepDown(wizard, message.SpellID, message.SpellIDOld)
            || StepDown(wizard, message.SpellIDOld, message.SpellID);

        Logger.Debug("Tiered spell refund between {SpellId} and {OldSpellId}: {Refunded}.",
            Logger.Args(message.SpellID, message.SpellIDOld, refunded));

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLREFUNDRESP {
            Success = ToSuccess(refunded),
            FailedRequirementIndex = TieredSpellLedger.NoFailedRequirement,
        });
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_TIEREDSPELLREFUNDFORMP))]
    private void ReceiveTieredSpellRefundForMultiplePaths(WIZARD3_56_PROTOCOL.MSG_TIEREDSPELLREFUNDFORMP message) {
        var wizard = GetActiveWizard();
        var spellIdList = message.SpellIDsToRefund.ToString();
        var refunded = RefundSpells(wizard, message.SpellID, ParseSpellIds(spellIdList));

        Logger.Debug("Tiered spell refund of {SpellIds} for {SpellId}: {Refunded}.",
            Logger.Args(spellIdList, message.SpellID, refunded));

        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_TIEREDSPELLREFUNDFORMPRESP { Success = ToSuccess(refunded) });
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_TIEREDSPELLDOPREPFORMP))]
    private void ReceiveTieredSpellDoPrepForMultiplePaths(WIZARD3_56_PROTOCOL.MSG_TIEREDSPELLDOPREPFORMP message) {
        var wizard = GetActiveWizard();
        var prepped = PrepForMultiplePaths(wizard, message.SpellID, message.SpellIDOld, message.KeepProgression != 0);

        Logger.Debug("Tiered spell prep for multiple paths of {SpellId} ({OldSpellId}), keep {Keep}: {Prepped}.",
            Logger.Args(message.SpellID, message.SpellIDOld, message.KeepProgression, prepped));

        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_TIEREDSPELLDOPREPFORMPRESP { Success = ToSuccess(prepped) });
    }

    private TieredSpellUpgradeDecision Upgrade(Wizard wizard, int groupIndex, uint spellId, uint newSpellId) {
        var group = TieredSpellIndex.Instance.Groups[groupIndex];
        if (CoreObjectFactory.GetCoreTemplate(spellId) is not TieredSpellTemplate current
            || CoreObjectFactory.GetCoreTemplate(newSpellId) is not TieredSpellTemplate next
            || SpellFactory.GetSpell(newSpellId) is not { } newSpell) {
            return NotUpgraded(TieredSpellUpgradeOutcome.NotNextTier);
        }

        TieredSpellUpgradeDecision decision;
        ClientReagentItem shards = null;
        lock (HousingItems.BackpackLock(wizard)) {
            var isTrained = wizard.SpellbookBehavior.HasSpell(spellId) && !wizard.SpellbookBehavior.HasSpell(newSpellId);
            var shardsOwned = wizard.AlchemyBehavior.GetReagent(group.ShardTemplateId)?.m_quantity ?? 0;
            decision = TieredSpellLedger.DecideUpgrade(isTrained, current, next,
                FirstFailedRequirement(wizard, next.m_requirements), shardsOwned);
            if (decision.Outcome != TieredSpellUpgradeOutcome.Upgraded) {
                return decision;
            }

            if (decision.Cost > 0 && !wizard.RemoveReagents(group.ShardTemplateId, decision.Cost, out shards)) {
                return NotUpgraded(TieredSpellUpgradeOutcome.NotEnoughShards);
            }

            var progress = wizard.TieredSpells.Find(candidate => candidate.GroupIndex == groupIndex)
                ?? StartProgress(wizard, groupIndex, spellId);
            wizard.UnlearnSpell(spellId);
            wizard.LearnSpell(newSpell);
            TieredSpellLedger.ApplyUpgrade(progress, spellId, newSpellId, decision.Cost);
            wizard.SaveTieredSpells();
        }

        if (shards is not null) {
            SendShardStack(wizard, shards);
        }

        SendSpellSwap(spellId, newSpellId);

        return decision;
    }

    private bool StepDown(Wizard wizard, uint currentSpellId, uint targetSpellId) {
        if (SpellFactory.GetSpell(targetSpellId) is not { } targetSpell) {
            return false;
        }

        int shardsReturned;
        IndexedTieredSpellGroup group;
        lock (HousingItems.BackpackLock(wizard)) {
            var progress = wizard.TieredSpells.Find(candidate => candidate.CurrentSpellId == currentSpellId);
            if (progress is null) {
                return false;
            }

            group = TieredSpellIndex.Instance.Groups[progress.GroupIndex];
            if (!TieredSpellLedger.TryRefund(progress, currentSpellId, targetSpellId, group.ReturnRate, out shardsReturned)) {
                return false;
            }

            wizard.UnlearnSpell(currentSpellId);
            wizard.LearnSpell(targetSpell);
            wizard.SaveTieredSpells();
        }

        ReturnShards(wizard, group, shardsReturned);
        SendSpellSwap(currentSpellId, targetSpellId);

        return true;
    }

    private bool RefundSpells(Wizard wizard, uint spellId, IReadOnlyCollection<uint> spellIds) {
        uint previousSpellId;
        uint currentSpellId;
        int shardsReturned;
        IndexedTieredSpellGroup group;
        lock (HousingItems.BackpackLock(wizard)) {
            var progress = wizard.TieredSpells.Find(candidate => IsTierOf(candidate, spellId));
            if (progress is null) {
                return false;
            }

            group = TieredSpellIndex.Instance.Groups[progress.GroupIndex];
            if (wizard.GameStats.m_currentGold < group.GoldResetCost) {
                return false;
            }

            var paysCrowns = group.GoldResetCost == 0 && group.CrownsResetCost > 0;
            if (paysCrowns && !wizard.Account.TryAdjustCrowns(-(int) group.CrownsResetCost)) {
                return false;
            }

            previousSpellId = progress.CurrentSpellId;
            if (!TieredSpellLedger.TryRefundSpells(progress, spellIds, group.ReturnRate, out shardsReturned)) {
                if (paysCrowns && !wizard.Account.TryAdjustCrowns((int) group.CrownsResetCost)) {
                    Logger.Error("Could not return {Crowns} reset crowns to account {AccountId}.",
                        Logger.Args(group.CrownsResetCost, wizard.AccountId));
                }

                return false;
            }

            if (group.GoldResetCost > 0) {
                wizard.RemoveGold((int) group.GoldResetCost);
            }

            currentSpellId = progress.CurrentSpellId;
            SwapTrainedSpell(wizard, previousSpellId, currentSpellId);
            wizard.SaveTieredSpells();
        }

        ReturnShards(wizard, group, shardsReturned);
        SendSpellSwap(previousSpellId, currentSpellId);

        return true;
    }

    private bool PrepForMultiplePaths(Wizard wizard, uint spellId, uint oldSpellId, bool keepProgression) {
        uint previousSpellId;
        uint currentSpellId;
        int shardsReturned;
        IndexedTieredSpellGroup group;
        lock (HousingItems.BackpackLock(wizard)) {
            var progress = wizard.TieredSpells.Find(candidate => IsTierOf(candidate, spellId) || IsTierOf(candidate, oldSpellId))
                ?? StartProgressForTrainedTier(wizard, spellId, oldSpellId);
            if (progress is null) {
                return false;
            }

            group = TieredSpellIndex.Instance.Groups[progress.GroupIndex];
            previousSpellId = progress.CurrentSpellId;
            shardsReturned = TieredSpellLedger.PrepForMultiplePaths(progress, keepProgression, group.ReturnRate);
            currentSpellId = progress.CurrentSpellId;
            SwapTrainedSpell(wizard, previousSpellId, currentSpellId);
            wizard.SaveTieredSpells();
        }

        ReturnShards(wizard, group, shardsReturned);
        if (previousSpellId != currentSpellId) {
            SendSpellSwap(previousSpellId, currentSpellId);
        }

        return true;
    }

    private void SendVisibility(Wizard wizard, byte checkTieredSpellNotifications, GID shardType) {
        var groups = TieredSpellIndex.Instance.Groups;
        var trainedSpells = TrainedSpellNames(wizard);
        var visibility = TieredSpellVisibility.For(groups, trainedSpells, wizard.AlchemyBehavior.HasReageant,
            group => CanUpgrade(wizard, group, trainedSpells));
        var data = TieredSpellVisibility.Serialize(visibility);

        Logger.Debug("Tiered spell group visibility for {Count} groups is {Length} bytes.",
            Logger.Args(groups.Count, data.Length));

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_CANSEETIEREDSPELLGROUPS {
            Visibility = data,
            CheckTieredSpellNotifications = checkTieredSpellNotifications,
            ShardType = shardType,
        });
    }

    private int FirstFailedRequirement(Wizard wizard, RequirementList requirements) {
        var context = new GenericRequirementContext(requirements, SessionActor.ActorRef, null, wizard);
        if (RequirementDispatcher.EvaluateRequirements(requirements, context)) {
            return TieredSpellLedger.NoFailedRequirement;
        }

        var index = TieredSpellLedger.FirstFailedRequirement(requirements, requirement => requirement is RequirementList nested
            ? RequirementDispatcher.EvaluateRequirements(nested, context)
            : RequirementDispatcher.EvaluateIndividualRequirement(requirement, context));

        // An OR list can fail as a whole while each item alone passes; the client still needs an index.
        return Math.Max(index, 0);
    }

    private void ReturnShards(Wizard wizard, IndexedTieredSpellGroup group, int shardsReturned) {
        if (shardsReturned <= 0) {
            return;
        }

        var granted = LootGranter.GrantReagent(SessionActor.ActorRef, wizard, group.ShardTemplateId, shardsReturned);
        if (granted < shardsReturned) {
            Logger.Warning("Returned {Granted} of {Count} shards {ShardId} to {CharId}: the stack is full.",
                Logger.Args(granted, shardsReturned, group.ShardTemplateId, wizard.CharId));
        }
    }

    private void SendShardStack(Wizard wizard, ClientReagentItem shards) {
        if (shards.m_quantity > 0) {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE {
                GlobalID = wizard.GameObjectID,
                ItemID = shards.m_globalID,
                Quantity = shards.m_quantity,
            });

            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTREMOVE {
            GlobalID = wizard.GameObjectID,
            ItemID = shards.m_globalID,
        });
    }

    private void SendSpellSwap(uint oldSpellId, uint newSpellId) {
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMBOOK { SpellID = (int) oldSpellId });
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK { SpellID = (int) newSpellId });
    }

    private static void SwapTrainedSpell(Wizard wizard, uint oldSpellId, uint newSpellId) {
        if (oldSpellId == newSpellId || SpellFactory.GetSpell(newSpellId) is not { } newSpell) {
            return;
        }

        wizard.UnlearnSpell(oldSpellId);
        wizard.LearnSpell(newSpell);
    }

    private static TieredSpellProgress StartProgress(Wizard wizard, int groupIndex, uint currentSpellId) {
        var tierOne = SpellFactory.GetSpell(TieredSpellIndex.Instance.Groups[groupIndex].TierOneSpell);
        var progress = new TieredSpellProgress {
            GroupIndex = groupIndex,
            TierOneSpellId = tierOne?.m_templateID ?? currentSpellId,
            CurrentSpellId = currentSpellId,
            DonePrepForMultiplePaths = true,
        };
        wizard.TieredSpells.Add(progress);

        return progress;
    }

    private static TieredSpellProgress StartProgressForTrainedTier(Wizard wizard, params uint[] spellIds) {
        var groups = TieredSpellIndex.Instance.Groups;
        foreach (var spellId in spellIds.Where(wizard.SpellbookBehavior.HasSpell)) {
            var name = (CoreObjectFactory.GetCoreTemplate(spellId) as SpellTemplate)?.m_name.ToString();
            var groupIndex = groups.ToList().FindIndex(group => group.Spells.Contains(name));
            if (groupIndex >= 0) {
                return StartProgress(wizard, groupIndex, spellId);
            }
        }

        return null;
    }

    private static bool IsTierOf(TieredSpellProgress progress, uint spellId)
        => progress.TierOneSpellId == spellId || progress.CurrentSpellId == spellId;

    private static bool CanUpgrade(Wizard wizard, IndexedTieredSpellGroup group, IReadOnlySet<string> trainedSpells) {
        var shardsOwned = wizard.AlchemyBehavior.GetReagent(group.ShardTemplateId)?.m_quantity ?? 0;

        return group.Spells.Where(trainedSpells.Contains)
            .Select(TierTemplate)
            .SelectMany(tier => tier?.m_nextTierSpells ?? [])
            .Select(name => TierTemplate(name.ToString()))
            .Any(next => next is { m_retired: false } && next.m_newShardCost <= shardsOwned);
    }

    private static TieredSpellTemplate TierTemplate(string spellName)
        => SpellFactory.TryGetTemplateHash(spellName, out var hash)
            ? SpellFactory.s_spellTemplates[hash] as TieredSpellTemplate
            : null;

    private static uint[] ParseSpellIds(string list)
        => [.. list.Split(s_listSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => uint.TryParse(id, out var spellId) ? spellId : 0)
            .Where(spellId => spellId != 0)];

    private static TieredSpellUpgradeDecision NotUpgraded(TieredSpellUpgradeOutcome outcome)
        => new(outcome, 0, TieredSpellLedger.NoFailedRequirement);

    private static byte ToSuccess(bool succeeded) => (byte) (succeeded ? 1 : 0);

    private static HashSet<string> TrainedSpellNames(Wizard wizard) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var templateId in wizard.SpellbookBehavior.LearnedSpellTemplateIds) {
            if (CoreObjectFactory.GetCoreTemplate(templateId) is SpellTemplate spell) {
                names.Add(spell.m_name.ToString());
            }
        }

        return names;
    }

}
