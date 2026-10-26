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
 * Shards, gold, the spellbook, the decks and the progress change together under the wizard's backpack lock.
 * The paid reset (D26) is REFUNDFORMP; the free one-time reset is DOPREPFORMP.
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
        string data;
        lock (HousingItems.BackpackLock(wizard)) {
            data = string.Join(ListSeparator, wizard.TieredSpells.Where(progress => progress.DonePrepForMultiplePaths)
                .Select(progress => progress.TierOneSpellId));
        }

        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_UPDATETSDONEPREPFORMP { Data = data });
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTTSMARKEDASUNTRAINED))]
    private void ReceiveRequestMarkedAsUntrained(WIZARD3_56_PROTOCOL.MSG_REQUESTTSMARKEDASUNTRAINED _) {
        var wizard = GetActiveWizard();
        string data;
        lock (HousingItems.BackpackLock(wizard)) {
            data = string.Join(ListSeparator, wizard.TieredSpells
                .Where(progress => !wizard.SpellbookBehavior.HasSpell(progress.CurrentSpellId))
                .Select(progress => progress.CurrentSpellId));
        }

        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_UPDATETSMARKEDASUNTRAINED { Data = data });
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLUPGRADE))]
    private void ReceiveTieredSpellUpgrade(WIZARD2_53_PROTOCOL.MSG_TIEREDSPELLUPGRADE message) {
        var wizard = GetActiveWizard();
        var groups = TieredSpellIndex.Instance.Groups;
        var decision = message.TieredSpellGroup < groups.Count
            ? Upgrade(wizard, (int) message.TieredSpellGroup, message.SpellID, message.SpellIDNew)
            : NotUpgraded(TieredSpellUpgradeOutcome.WrongGroup);

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
        var refunded = StepDown(wizard, message.SpellID, message.SpellIDOld);

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
        var groups = TieredSpellIndex.Instance.Groups;
        if (CoreObjectFactory.GetCoreTemplate(spellId) is not TieredSpellTemplate current
            || CoreObjectFactory.GetCoreTemplate(newSpellId) is not TieredSpellTemplate next
            || SpellFactory.GetSpell(newSpellId) is not { } newSpell) {
            return NotUpgraded(TieredSpellUpgradeOutcome.NotNextTier);
        }

        if (TieredSpellLedger.ResolveGroup(groups, current.m_name.ToString(), next.m_name.ToString()) != groupIndex) {
            return NotUpgraded(TieredSpellUpgradeOutcome.WrongGroup);
        }

        var group = groups[groupIndex];
        if (TierOneSpellId(group) is not { } tierOneSpellId) {
            return NotUpgraded(TieredSpellUpgradeOutcome.NotTrained);
        }

        TieredSpellUpgradeDecision decision;
        ClientReagentItem shards = null;
        lock (HousingItems.BackpackLock(wizard)) {
            var isTrained = wizard.SpellbookBehavior.HasSpell(spellId) && !wizard.SpellbookBehavior.HasSpell(newSpellId);
            var shardsOwned = wizard.AlchemyBehavior.GetReagent(group.ShardTemplateId)?.m_quantity ?? 0;
            decision = TieredSpellLedger.DecideUpgrade(group, isTrained, current, next,
                FirstFailedRequirement(wizard, next.m_requirements), shardsOwned);
            if (decision.Outcome != TieredSpellUpgradeOutcome.Upgraded) {
                return decision;
            }

            var existing = FindProgress(wizard, groupIndex);
            var progress = existing ?? TieredSpellLedger.NewProgress(groupIndex, tierOneSpellId);
            if (!TieredSpellLedger.CanApplyUpgrade(progress, spellId, newSpellId) || !wizard.LearnSpell(newSpell)) {
                return NotUpgraded(TieredSpellUpgradeOutcome.NotTrained);
            }

            if (decision.Cost > 0 && !wizard.RemoveReagents(group.ShardTemplateId, decision.Cost, out shards)) {
                wizard.UnlearnSpell(newSpellId);

                return NotUpgraded(TieredSpellUpgradeOutcome.NotEnoughShards);
            }

            wizard.UnlearnSpell(spellId);
            if (existing is null) {
                wizard.TieredSpells.Add(progress);
            }

            TieredSpellLedger.ApplyUpgrade(progress, spellId, newSpellId, decision.Cost);
            wizard.SaveTieredSpells();
        }

        if (shards is not null) {
            SendShardStack(wizard, shards);
        }

        SendSpellSwap(spellId, newSpellId);

        return decision;
    }

    private bool StepDown(Wizard wizard, uint spellId, uint otherSpellId) {
        var groupIndex = GroupOfSpells(spellId, otherSpellId);
        if (groupIndex == TieredSpellLedger.NoGroup) {
            return false;
        }

        var group = TieredSpellIndex.Instance.Groups[groupIndex];
        lock (HousingItems.BackpackLock(wizard)) {
            var progress = FindProgress(wizard, groupIndex);
            if (progress is null || (progress.CurrentSpellId != spellId && progress.CurrentSpellId != otherSpellId)) {
                return false;
            }

            var targetSpellId = progress.CurrentSpellId == spellId ? otherSpellId : spellId;
            var draft = TieredSpellLedger.Copy(progress);
            if (!TieredSpellLedger.TryRefund(draft, progress.CurrentSpellId, targetSpellId, group.ReturnRate, out var shardsReturned)
                || !CommitPathChange(wizard, progress, draft, group, shardsReturned, () => true)) {
                return false;
            }

            wizard.SaveTieredSpells();
        }

        return true;
    }

    private bool RefundSpells(Wizard wizard, uint spellId, IReadOnlyCollection<uint> spellIds) {
        var groupIndex = GroupOfSpells(spellId, spellId);
        if (groupIndex == TieredSpellLedger.NoGroup) {
            return false;
        }

        var group = TieredSpellIndex.Instance.Groups[groupIndex];
        lock (HousingItems.BackpackLock(wizard)) {
            var progress = FindProgress(wizard, groupIndex);
            if (progress is null) {
                return false;
            }

            var draft = TieredSpellLedger.Copy(progress);
            if (!TieredSpellLedger.TryRefundSpells(draft, spellIds, group.ReturnRate, out var shardsReturned)
                || !CommitPathChange(wizard, progress, draft, group, shardsReturned, () => TryChargeReset(wizard, group))) {
                return false;
            }

            wizard.SaveTieredSpells();
        }

        return true;
    }

    private bool PrepForMultiplePaths(Wizard wizard, uint spellId, uint oldSpellId, bool keepProgression) {
        var groupIndex = GroupOfSpells(spellId, spellId);
        if (groupIndex == TieredSpellLedger.NoGroup) {
            groupIndex = GroupOfSpells(oldSpellId, oldSpellId);
        }

        if (groupIndex == TieredSpellLedger.NoGroup) {
            return false;
        }

        var group = TieredSpellIndex.Instance.Groups[groupIndex];
        lock (HousingItems.BackpackLock(wizard)) {
            var existing = FindProgress(wizard, groupIndex);
            var progress = existing ?? StartAtTrainedTierOne(wizard, groupIndex, group);
            if (progress is null) {
                return false;
            }

            var draft = TieredSpellLedger.Copy(progress);
            if (!TieredSpellLedger.TryPrepForMultiplePaths(draft, keepProgression, group.ReturnRate, out var shardsReturned)
                || !CommitPathChange(wizard, progress, draft, group, shardsReturned, () => true)) {
                return false;
            }

            if (existing is null) {
                wizard.TieredSpells.Add(progress);
            }

            wizard.SaveTieredSpells();
        }

        return true;
    }

    /// <summary>
    /// Applies a planned path change under the caller's backpack lock: every check runs before the first change,
    /// then the charge, the spellbook swap, the shard return, the record and the decks, in that order.
    /// </summary>
    /// <returns>False, with nothing changed, when a check or the charge fails.</returns>
    private bool CommitPathChange(Wizard wizard, TieredSpellProgress progress, TieredSpellProgress draft,
        IndexedTieredSpellGroup group, int shardsReturned, Func<bool> tryCharge) {
        var oldSpellId = progress.CurrentSpellId;
        var newSpellId = draft.CurrentSpellId;
        var isSwap = oldSpellId != newSpellId;
        var newSpell = isSwap ? SpellFactory.GetSpell(newSpellId) : null;
        if (isSwap && newSpell is null) {
            return false;
        }

        if (shardsReturned > 0 && !wizard.AlchemyBehavior.HasReagentSpace(group.ShardTemplateId, shardsReturned)) {
            return false;
        }

        if (!tryCharge()) {
            return false;
        }

        var swapped = !isSwap || TieredSpellLedger.TrySwapSpell(
            () => wizard.SpellbookBehavior.HasSpell(newSpellId) || wizard.LearnSpell(newSpell),
            () => UnlearnIfTrained(wizard, oldSpellId));
        if (!swapped) {
            Logger.Error("Could not teach tier {SpellId} to {CharId} after the refund was charged.",
                Logger.Args(newSpellId, wizard.CharId));

            return false;
        }

        if (shardsReturned > 0 && !LootGranter.GrantReagents(SessionActor.ActorRef, wizard, group.ShardTemplateId, shardsReturned)) {
            Logger.Error("Could not return {Count} shards {ShardId} to {CharId}.",
                Logger.Args(shardsReturned, group.ShardTemplateId, wizard.CharId));
        }

        var removedTiers = TieredSpellLedger.RemovedTiers(progress, draft);
        progress.CurrentSpellId = draft.CurrentSpellId;
        progress.Unlocks = draft.Unlocks;
        progress.DonePrepForMultiplePaths = draft.DonePrepForMultiplePaths;

        ReplaceTiersInDecks(wizard, removedTiers, newSpellId);
        if (isSwap) {
            SendSpellSwap(oldSpellId, newSpellId);
        }

        return true;
    }

    private void ReplaceTiersInDecks(Wizard wizard, IReadOnlySet<uint> removedTiers, uint newSpellId) {
        if (removedTiers.Count == 0) {
            return;
        }

        foreach (var deck in wizard.InventoryBehavior.Items.Concat(wizard.EquipmentBehavior.EquippedItems)) {
            if (!CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(deck, out var deckBehavior)) {
                continue;
            }

            foreach (var (templateId, quantity) in TieredSpellLedger.ReplacedDeckCards(deckBehavior.m_spellList ?? [], removedTiers)) {
                for (var copy = 0; copy < quantity && wizard.RemoveSpellFromDeck(templateId, deck.m_globalID); copy++) {
                    SendToSocket(new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK {
                        SpellID = (int) templateId,
                        DeckID = deck.m_globalID,
                        Success = 1,
                    });

                    if (wizard.AddSpellToDeck(newSpellId, deck.m_globalID)) {
                        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK {
                            SpellID = (int) newSpellId,
                            DeckID = deck.m_globalID,
                            Success = 1,
                        });
                    }
                }
            }
        }
    }

    private static bool TryChargeReset(Wizard wizard, IndexedTieredSpellGroup group) {
        if (group.GoldResetCost > 0) {
            if (wizard.GameStats.m_currentGold < group.GoldResetCost) {
                return false;
            }

            wizard.RemoveGold((int) group.GoldResetCost);

            return true;
        }

        return group.CrownsResetCost == 0 || wizard.Account.TryAdjustCrowns(-(int) group.CrownsResetCost);
    }

    private void SendVisibility(Wizard wizard, byte checkTieredSpellNotifications, GID shardType) {
        var groups = TieredSpellIndex.Instance.Groups;
        TieredSpellGroupVisibility visibility;
        lock (HousingItems.BackpackLock(wizard)) {
            var trainedSpells = TrainedSpellNames(wizard);
            var upgradable = UpgradableGroups(wizard, trainedSpells);
            visibility = TieredSpellVisibility.For(groups, trainedSpells, wizard.AlchemyBehavior.HasReageant,
                upgradable.Contains);
        }

        var data = TieredSpellVisibility.Serialize(visibility);

        Logger.Debug("Tiered spell group visibility for {Count} groups is {Length} bytes.",
            Logger.Args(groups.Count, data.Length));

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_CANSEETIEREDSPELLGROUPS {
            Visibility = data,
            CheckTieredSpellNotifications = checkTieredSpellNotifications,
            ShardType = shardType,
        });
    }

    private HashSet<IndexedTieredSpellGroup> UpgradableGroups(Wizard wizard, IReadOnlySet<string> trainedSpells) {
        var groups = TieredSpellIndex.Instance.Groups;
        var upgradable = new HashSet<IndexedTieredSpellGroup>(ReferenceEqualityComparer.Instance);
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++) {
            var group = groups[groupIndex];
            var currentName = CurrentTierName(wizard, FindProgress(wizard, groupIndex), group, trainedSpells);
            if (currentName is not null && CanUpgradeFrom(wizard, group, currentName)) {
                upgradable.Add(group);
            }
        }

        return upgradable;
    }

    private bool CanUpgradeFrom(Wizard wizard, IndexedTieredSpellGroup group, string currentName) {
        var shardsOwned = wizard.AlchemyBehavior.GetReagent(group.ShardTemplateId)?.m_quantity ?? 0;

        return (TierTemplate(currentName)?.m_nextTierSpells ?? [])
            .Select(name => TierTemplate(name.ToString()))
            .Any(next => next is { m_retired: false }
                && next.m_newShardCost <= shardsOwned
                && FirstFailedRequirement(wizard, next.m_requirements) == TieredSpellLedger.NoFailedRequirement);
    }

    private static string CurrentTierName(Wizard wizard, TieredSpellProgress progress, IndexedTieredSpellGroup group,
        IReadOnlySet<string> trainedSpells) {
        if (progress is null) {
            return trainedSpells.Contains(group.TierOneSpell) ? group.TierOneSpell : null;
        }

        return wizard.SpellbookBehavior.HasSpell(progress.CurrentSpellId)
            ? (CoreObjectFactory.GetCoreTemplate(progress.CurrentSpellId) as SpellTemplate)?.m_name.ToString()
            : null;
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

    private static void UnlearnIfTrained(Wizard wizard, uint spellId) {
        if (wizard.SpellbookBehavior.HasSpell(spellId)) {
            wizard.UnlearnSpell(spellId);
        }
    }

    private static TieredSpellProgress StartAtTrainedTierOne(Wizard wizard, int groupIndex, IndexedTieredSpellGroup group)
        => TierOneSpellId(group) is { } tierOneSpellId && wizard.SpellbookBehavior.HasSpell(tierOneSpellId)
            ? TieredSpellLedger.NewProgress(groupIndex, tierOneSpellId)
            : null;

    private static TieredSpellProgress FindProgress(Wizard wizard, int groupIndex)
        => wizard.TieredSpells.Find(progress => progress.GroupIndex == groupIndex);

    private static int GroupOfSpells(uint spellId, uint otherSpellId)
        => TieredSpellLedger.ResolveGroup(TieredSpellIndex.Instance.Groups, SpellName(spellId), SpellName(otherSpellId));

    private static string SpellName(uint spellId)
        => (CoreObjectFactory.GetCoreTemplate(spellId) as SpellTemplate)?.m_name.ToString();

    private static uint? TierOneSpellId(IndexedTieredSpellGroup group)
        => SpellFactory.GetSpell(group.TierOneSpell)?.m_templateID;

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
