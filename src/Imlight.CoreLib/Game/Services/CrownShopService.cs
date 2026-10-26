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
 * CROWNSHOP SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Handles the functionality of the crown shop
 * 
 * 
 * USAGE EXAMPLE:
 *
 * 
 * NOTE:
 * 
 * 
 * TODO:
 * - Removing from wishlist (& Wishlist privacy)
 * - Daily Spiral quests
 * 
 * Created by: Phill030
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.CrownShop;
using Imlight.CoreLib.Game.Elixirs;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Game.Packs;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Imlight.CoreLib.Game.Services;

internal class CrownShopService(SessionActor sessionActor) : MessageService(sessionActor) {
    private static readonly uint s_maxEmotes
        = ConfigurationManager.Settings["Character.MaxEmotesAllowed"].AsUInt();
    private static readonly uint s_maxTeleportEffects
        = ConfigurationManager.Settings["Character.MaxTeleportsAllowed"].AsUInt();

    private const uint ReagentPropertyFlags =
        (uint) (PropertyFlags.Prop_Save | PropertyFlags.Prop_Copy | PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit); // 27

    private const uint GiftPropertyFlags =
        (uint) (PropertyFlags.Prop_Save | PropertyFlags.Prop_Copy | PropertyFlags.Prop_Public | PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit); // 31

    private static readonly CoreObjectSerializer s_coreObjectSerializer = new(behaviors: SerializerFlags.None);
    private static readonly ObjectSerializer s_nonVersionableSerializer = new(Versionable: false, Behaviors: SerializerFlags.None);
    private static readonly ObjectSerializer s_versionableSerializer = new(Versionable: true, Behaviors: SerializerFlags.None);

    private static readonly Dictionary<int, string> s_errorNames = [];
    private static readonly int s_errorNotEnoughCrowns = PurchaseError("Error_NotEnoughCrowns");
    private static readonly int s_errorCantAfford = PurchaseError("Error_BuyerCantAffordItem");
    private static readonly int s_errorInvFull = PurchaseError("Error_CantHoldItemInventoryFull");
    private static readonly int s_errorInvBankFull = PurchaseError("Error_CantHoldItemInventoryFullAndReachedBankCap");
    private static readonly int s_errorMaxGold = PurchaseError("Error_CantHoldItemMaxGold");
    private static readonly int s_errorMaxLunari = PurchaseError("CantHoldItemMaxLunari");
    private static readonly int s_errorMaxEmotes = PurchaseError("CantHoldItemMaxEmotes");
    private static readonly int s_errorMaxTeleportEffects = PurchaseError("CantHoldItemMaxTeleportEffects");
    private static readonly int s_errorInCombat = PurchaseError("Error_InCombat");
    private static readonly int s_errorNotInCombat = PurchaseError("Error_NotInCombat");
    private static readonly int s_errorLevelTooLow = PurchaseError("Error_LevelTooLow");
    private static readonly int s_errorOneCastle = PurchaseError("Error_OnlyOneOfEachIsland");
    private static readonly int s_errorMaxElixirs = PurchaseError("Error_MaxElixirs");
    private static readonly int s_errorCSDisabled = PurchaseError("Error_CrownShopDisabled");
    private static readonly int s_errorPurchase = PurchaseError("Error_PriceLockMismatch");
    private static readonly int s_errorNoGiftThisItem = PurchaseError("Error_NoGiftThisItem");
    private static readonly int s_errorFailedToSendGift = PurchaseError("Error_FailedToSendGift");
    private static readonly int s_errorNoPriceForType = PurchaseError("Error_PriceLockNoPriceForType");

    private static int PurchaseError(string name) {
        var code = (int) StringHash.Compute(name);
        s_errorNames[code] = name;
        return code;
    }

    private const string HenchmanSummonTimeoutKey = "HenchmanSummonTimeout";
    private static readonly TimeSpan s_henchmanSummonTimeout = TimeSpan.FromSeconds(10);

    private readonly HenchmanPurchaseFlow _henchmanPurchase = new();

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CrownShopService(parentActor));

    private static ulong GenerateRandomUlong() {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        Random.Shared.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST))]
    private void ReceiveCrownShopSegDataRequest(WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_REQUEST message) {
        var wizard = GetActiveWizard();
        var schoolStr = wizard.MagicSchoolBehavior.MagicSchool.ToString();
        var schoolCode = schoolStr.Length >= 2 ? schoolStr[..2] : schoolStr;

        var segmentationInputData = new SegmentationInputData {
            m_bIsValidSegmentationData = true,
            m_playerLevel = wizard.MagicSchoolBehavior.Level,
            m_playerSchoolOfFocus = schoolCode,
            m_accountNDaysAged = (int) (DateTime.Now - wizard.Account.CreationTime).TotalDays,
            m_accountNDaysSinceLastLogin = 0,
            m_accountNDaysSinceLastPurchase = 0,
            m_accountNDaysLastCrownsPurchase = 0,
            m_accountIsMember = 0,
            m_accountIsCSR = wizard.Account.AuthLevel > AuthLevel.None ? 1 : 0, // todo: Change this back before prod!
            m_accountNCrownsSpent = 0,
            m_accountNCrownsInWallet = wizard.Account.Crowns,
            m_accountNDaysSinceItemPurchased = [],
            m_numOfParticularItemInInventory = [],
            m_numItemsOfCategoryInInventory = [],
            m_playerHasBadge = [], // Used for calculating Requirements
            m_accountNHighestWorld = 0
        };

        if (!s_nonVersionableSerializer.Serialize(segmentationInputData, PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit, out var serializedData)) {
            Logger.Error("Failed to serialize SegmentationInputData.");
            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_SEGDATA_RESPONSE {
            Success = 1,
            Data = serializedData
        });

        // Sync the CrownShop Crown-Balance so it does not display 0.
        SyncCrownBalance(wizard);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST))]
    private void ReceiveCrownShopListRequest(WIZARD_12_PROTOCOL.MSG_PCS_LIST_REQUEST message) {
        var nextUpdateId = message.UpdateID == 0 ? 1u : message.UpdateID + 1;
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_LIST_RESPONSE {
            Data = CrownShopHandler.GetCrownShopData(),
            Updates = string.Empty,
            UpdateID = nextUpdateId,
            Error = 0
        });
    }

    // Is this saleID still valid and actively running?
    // Does this saleID grant the price the client is asking to lock
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST))]
    private void ReceivePriceLockReq(WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_REQUEST message) {
        Logger.Information("Received MSG_PCS_PRICE_LOCK_REQUEST for item {0}", Logger.Args(message.Item));

        if (message.SaleID != 0) {
            Logger.Warning("SaleID {0} is not supported. Ignoring.", Logger.Args(message.SaleID));
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_RESPONSE {
                Error = s_errorPurchase
            });
            return;
        }

        if (!CrownShopHandler.TryGetCrownShopItem(message.Item, out var item)) {
            Logger.Warning("Item {0} not found in CrownShop.", Logger.Args(message.Item));
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_RESPONSE {
                Error = s_errorPurchase
            });
            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PRICE_LOCK_RESPONSE {
            CostCrowns = item.m_crownsCost,
            CostGold = item.m_goldCost,
            CostTickets = 0,
            Error = 0,
            Item = message.Item
        });
    }

    // The client requests to buy [N amount] of this item (and possibly gift it to another player),
    // which will be removed(?) from the list of things they can buy (if enabled).
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST))]
    private void ReceivePurchaseRequest(WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message) {
        var wizard = GetActiveWizard();

        if (message.Count < 1 || message.Count > CrownShopHandler.s_maxBuyCount || message.SaleID != 0) {
            Logger.Warning("Rejected purchase request for item {0}.", Logger.Args(message.Item));
            SendPurchaseError(s_errorPurchase, message.Item, wizard.IsInDuel);
            return;
        }

        // Authoritative cost lookup from catalog
        if (!CrownShopHandler.TryGetCrownShopItem(message.Item, out var catalogItem)) {
            Logger.Warning("Item {0} not found in CrownShop.", Logger.Args(message.Item));
            SendPurchaseError(s_errorPurchase, message.Item, wizard.IsInDuel);
            return;
        }

        var route = HenchmanPurchase.Route(wizard.IsInDuel, catalogItem.m_combatOnly, CrownShopHandler.IsHenchman(message.Item));
        if (route == CrownShopPurchaseRoute.RefuseInCombat) {
            SendPurchaseError(s_errorInCombat, message.Item, wizard.IsInDuel);
            return;
        }

        if (route == CrownShopPurchaseRoute.RefuseNotInCombat) {
            SendPurchaseError(s_errorNotInCombat, message.Item, wizard.IsInDuel);
            return;
        }

        var isGift = message.Recipient != 0;
        if (isGift && catalogItem.m_noGift) {
            SendPurchaseError(s_errorNoGiftThisItem, message.Item, wizard.IsInDuel);
            return;
        }

        if (!CrownShopPrice.TryCompute(catalogItem, message.Type, message.Count, out var amountToPay)) {
            Logger.Warning("Item {0} has no price for currency type {1}.", Logger.Args(message.Item, message.Type));
            SendPurchaseError(s_errorNoPriceForType, message.Item, wizard.IsInDuel);
            return;
        }

        var itemTemplate = CoreObjectFactory.GetCoreTemplate(message.Item);
        if (route == CrownShopPurchaseRoute.SummonHenchman) {
            RequestHenchmanSummon(wizard, message, itemTemplate, amountToPay);
            return;
        }

        if (!isGift && !HasInventorySpaceFor(wizard, itemTemplate, message.Count)) {
            SendPurchaseError(s_errorInvFull, message.Item, wizard.IsInDuel);
            return;
        }

        var error = GrantTransaction.Run(
            reserve: () => TryDebit(wizard, message.Type, amountToPay),
            release: () => Refund(wizard, message.Type, amountToPay),
            grant: transaction => isGift
                ? TryDeliverGift(wizard, message)
                : TryGrantPurchasedItem(transaction, wizard, message, itemTemplate));

        if (error != 0) {
            SendPurchaseError(error, message.Item, wizard.IsInDuel);
            return;
        }

        if (message.Type == CrownShopPrice.GoldCurrency) {
            SyncGoldBalance(wizard);
        }
        else {
            SyncCrownBalance(wizard);
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
            Item = message.Item,
            Error = 0,
            Cost = amountToPay,
            Count = message.Count,
            Gifted = isGift ? (byte) 1 : (byte) 0,
            Type = message.Type
        });

        if (!isGift && WishlistData.TryRemoveItem(wizard.CrownShopWishlist, (uint) message.Item, out var updatedWishlist)) {
            ApplyLocalWishlistUpdate(wizard, updatedWishlist, message.Item.ToString(), sendToSelf: true);
        }
    }

    private void RequestHenchmanSummon(Wizard wizard, WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message,
                                       CoreTemplate itemTemplate, int amountToPay) {
        if (message.Count != 1 || !HenchmanPurchase.TryGetCreatureTemplateId(itemTemplate, out var creatureTid)) {
            SendPurchaseError(s_errorPurchase, message.Item, wizard.IsInDuel);
            return;
        }

        if (_henchmanPurchase.IsPending) {
            Logger.Warning("Henchman purchase of item {0} refused: another henchman purchase is in flight.",
                Logger.Args(message.Item));
        }

        var purchaseId = GenerateRandomUlong() | 1;
        var error = _henchmanPurchase.Begin(purchaseId,
            () => TryDebit(wizard, message.Type, amountToPay),
            () => Refund(wizard, message.Type, amountToPay));
        if (error != 0) {
            SendPurchaseError(error, message.Item, wizard.IsInDuel);
            return;
        }

        var request = new COMBAT_106_PROTOCOL.MSG_SUMMONHENCHMAN {
            Owner = SessionActor.ActorRef,
            CreatureTid = creatureTid,
            IsPvPEnabled = HenchmanPurchase.IsPvPEnabled(itemTemplate),
            PurchaseId = purchaseId,
            Item = message.Item,
            Currency = message.Type,
            Cost = amountToPay
        };
        // Without an answer (a duel actor that stopped) the purchase would stay pending and paid; refund it instead.
        Timers.StartSingleTimer(HenchmanSummonTimeoutKey, new COMBAT_106_PROTOCOL.MSG_HENCHMANSUMMONED {
            Request = request,
            Result = HenchmanSummonResult.Failed,
            SubCircle = -1
        }, s_henchmanSummonTimeout);
        TellOtherServices(request);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_HENCHMANSUMMONED))]
    private void ReceiveHenchmanSummoned(COMBAT_106_PROTOCOL.MSG_HENCHMANSUMMONED message) {
        var wizard = GetActiveWizard();
        var request = message.Request;
        if (!_henchmanPurchase.TryFinish(request.PurchaseId, message.Result, out var error)) {
            Logger.Warning("Henchman purchase {0} of item {1} answered {2} after it was settled.",
                Logger.Args(request.PurchaseId, request.Item, message.Result));
            if (message.Result == HenchmanSummonResult.Summoned) {
                message.DuelActor.Tell(new COMBAT_106_PROTOCOL.MSG_DISMISSMINION {
                    SubCircle = message.SubCircle,
                    PurchaseId = request.PurchaseId
                });
            }
            return;
        }

        Timers.Cancel(HenchmanSummonTimeoutKey);
        if (error != 0) {
            Logger.Warning("Henchman summon for item {0} ended {1}; payment refunded.", Logger.Args(request.Item, message.Result));
            SendPurchaseError(error, request.Item, wizard.IsInDuel);
            return;
        }

        if (request.Currency == CrownShopPrice.GoldCurrency) {
            SyncGoldBalance(wizard);
        }
        else {
            SyncCrownBalance(wizard);
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
            Item = request.Item,
            Error = 0,
            Cost = request.Cost,
            Count = 1,
            Gifted = 0,
            Type = request.Currency
        });
    }

    protected override void OnPreDispose() {
        Timers.Cancel(HenchmanSummonTimeoutKey);
        if (_henchmanPurchase.TryAbandon()) {
            Logger.Warning("Session {SessionId} closed with a henchman purchase in flight; payment refunded.",
                Logger.Args(SessionActor.SessionID));
        }

        base.OnPreDispose();
    }

    private static bool HasInventorySpaceFor(Wizard wizard, CoreTemplate itemTemplate, int count) {
        var requiredSlots = GiftingService.CalculateRequiredInventorySlots(itemTemplate, count);
        return requiredSlots == 0 || wizard.InventoryBehavior.HasInventorySpace(requiredSlots);
    }

    private static int TryDebit(Wizard wizard, int currency, int amount) {
        if (currency == CrownShopPrice.GoldCurrency) {
            if (wizard.GameStats.m_currentGold < amount) {
                return s_errorCantAfford;
            }

            wizard.RemoveGold(amount);
            return 0;
        }

        return wizard.Account.TryAdjustCrowns(-amount) ? 0 : s_errorNotEnoughCrowns;
    }

    private static void Refund(Wizard wizard, int currency, int amount) {
        if (currency == CrownShopPrice.GoldCurrency) {
            wizard.AddGold(amount);
            return;
        }

        if (!wizard.Account.TryAdjustCrowns(amount)) {
            Logger.Error("Could not save the refund of {0} crowns for account {1}.", Logger.Args(amount, wizard.AccountId));
        }
    }

    private int TryGrantPurchasedItem(GrantTransaction transaction,
                                      Wizard wizard,
                                      WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message,
                                      CoreTemplate itemTemplate) {
        switch (itemTemplate) {
            // Pack loot is rolled and cannot be taken back, so packs are the whole grant and opened last.
            case BoosterPackTemplate:
                for (var i = 0; i < message.Count; i++) {
                    PackManager.OpenPack(SessionActor.ActorRef, wizard, message.Item);
                }
                return 0;

            case GoldAmountTemplate goldItem:
                var gold = goldItem.m_goldAmount * message.Count;
                if (wizard.GameStats.m_currentGold + gold >= wizard.GameStats.m_baseGoldPouch) {
                    return s_errorMaxGold;
                }

                wizard.AddGold(gold);
                transaction.OnRollback(() => wizard.RemoveGold(gold));
                return 0;

            case LunariAmountTemplate lunariTemplate:
                var lunari = lunariTemplate.m_lunariAmount * message.Count;
                if (wizard.GameStats.m_currentEventCurrency1 + lunari >= wizard.GameStats.m_baseEventCurrency1Pouch) {
                    return s_errorMaxLunari;
                }

                wizard.AddLunari(lunari);
                transaction.OnRollback(() => wizard.RemoveLunari(lunari));
                transaction.OnCommit(() => SendToSocket(new WIZARD2_53_PROTOCOL.MSG_UPDATEEVENTCURRENCY1 {
                    EventCurrency1 = wizard.GameStats.m_currentEventCurrency1,
                    MaxEventCurrency1 = wizard.GameStats.m_baseEventCurrency1Pouch
                }));
                return 0;

            case ReagentItemTemplate reagentTemplate:
                return TryGrantReagent(transaction, wizard, reagentTemplate.m_templateID, message.Count);

            case PetSnackItemTemplate petSnackTemplate:
                return TryGrantPetSnack(transaction, wizard, petSnackTemplate.m_templateID, message.Count);

            case TourneyTokensAmountTemplate:
                Logger.Warning("Tourney tokens cannot be granted yet; refusing item {0}.", Logger.Args(message.Item));
                return s_errorPurchase;

            case ItemBundleTemplate itemBundle:
                for (var i = 0; i < message.Count; i++) {
                    foreach (var templateId in itemBundle.m_bundleItems) {
                        var bundleError = CoreObjectFactory.GetCoreTemplate(templateId) switch {
                            ReagentItemTemplate => TryGrantReagent(transaction, wizard, templateId, 1),
                            PetSnackItemTemplate => TryGrantPetSnack(transaction, wizard, templateId, 1),
                            _ => TryAddItemToInventory(transaction, wizard, templateId, message.Texture, message.Decal)
                                ? 0
                                : s_errorInvFull,
                        };
                        if (bundleError != 0) {
                            return bundleError;
                        }
                    }
                }
                return 0;

            case WizItemTemplate wizItemTemplate:
                return TryGrantWizItem(transaction, wizard, message, wizItemTemplate);

            default:
                Logger.Warning("Unhandled TemplateID: {0}", Logger.Args(message.Item));
                return s_errorPurchase;
        }
    }

    private int TryGrantReagent(GrantTransaction transaction, Wizard wizard, ulong reagentTemplateId, int count) {
        ClientReagentItem reagentObj = null;
        for (var i = 0; i < count; i++) {
            if (!wizard.AddReagent(reagentTemplateId, out reagentObj)) {
                Logger.Warning("Could not add reagent {0} to inventory.", Logger.Args(reagentTemplateId));
                return s_errorInvFull;
            }

            var added = reagentObj;
            transaction.OnRollback(() => wizard.RemoveReagent(added.m_globalID, out _));
        }

        if (reagentObj is null || !s_coreObjectSerializer.Serialize(reagentObj, ReagentPropertyFlags, out var serializedReagentObj)) {
            Logger.Warning("Failed to serialize reagent core object {0}.", Logger.Args(reagentTemplateId));
            return s_errorPurchase;
        }

        transaction.OnCommit(() => {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTADD {
                GlobalID = wizard.GameObjectID,
                Data = serializedReagentObj
            });

            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
                ItemGlobalID = reagentObj.m_globalID,
                ItemTemplateID = (uint) reagentObj.m_templateID,
                ItemLocation = 1
            });

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_REAGENTUPDATE {
                GlobalID = wizard.GameObjectID,
                ItemID = reagentObj.m_globalID,
                Quantity = reagentObj.m_quantity
            });
        });

        return 0;
    }

    private int TryGrantPetSnack(GrantTransaction transaction, Wizard wizard, ulong snackTemplateId, int count) {
        ClientPetSnackItem snackObj = null;
        for (var i = 0; i < count; i++) {
            if (!wizard.AddSnack(snackTemplateId, out snackObj)) {
                Logger.Warning("Could not add pet snack {0} to snack bag.", Logger.Args(snackTemplateId));
                return s_errorInvFull;
            }

            var added = snackObj;
            transaction.OnRollback(() => wizard.RemoveSnack(added.m_globalID, out _));
        }

        if (snackObj is null || !s_coreObjectSerializer.Serialize(snackObj, 24, out var serializedSnackObj)) {
            Logger.Warning("Failed to serialize pet snack core object {0}.", Logger.Args(snackTemplateId));
            return s_errorPurchase;
        }

        transaction.OnCommit(() => {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKADD {
                GlobalID = wizard.GameObjectID,
                Data = serializedSnackObj
            });

            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
                ItemGlobalID = snackObj.m_globalID,
                ItemTemplateID = (uint) snackObj.m_templateID,
                ItemLocation = 1
            });

            SendToSocket(new PET_9_PROTOCOL.MSG_PETSNACKUPDATE {
                GlobalID = wizard.GameObjectID,
                ItemID = snackObj.m_globalID,
                Quantity = snackObj.m_quantity
            });
        });

        return 0;
    }

    private int TryGrantWizItem(GrantTransaction transaction,
                                Wizard wizard,
                                WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message,
                                WizItemTemplate wizItemTemplate) {
        var behaviors = wizItemTemplate.m_behaviors;
        if (behaviors is not null) {
            if (message.PurchaseElixirEquipNow > 0) {
                // Equipping uses one copy and is charged for every copy, so equip-now buys exactly one.
                if (message.Count != 1) {
                    return s_errorPurchase;
                }

                if (MeetsEquipRequirements(wizard, wizItemTemplate)) {
                    var levelUpOrWorldElixir = behaviors.OfType<LevelUpElixirBehaviorTemplate>().FirstOrDefault();
                    if (levelUpOrWorldElixir is not null) {
                        if (!LevelUpElixirHelper.IsBelowTargetLevel(wizard, levelUpOrWorldElixir)) {
                            return s_errorPurchase;
                        }

                        var slotsNeeded = LevelUpElixirHelper.InventorySlotsNeeded(levelUpOrWorldElixir, wizard.MagicSchoolBehavior.MagicSchool.ToString());
                        if (slotsNeeded > 0 && !wizard.InventoryBehavior.HasInventorySpace(slotsNeeded)) {
                            return s_errorInvFull;
                        }

                        LevelUpElixirHelper.ApplyElixir(
                            wizard,
                            levelUpOrWorldElixir,
                            SendToSocket,
                            (msg, selfless, target) => ZoneBroadcast(msg, selfless, target),
                            (zone, effects, isPrivate, ownerCharId, location) => Teleport(zone, effects, isPrivate, ownerCharId, location),
                            PlaySound
                        );
                        return 0;
                    }

                    var elixirBehavior = behaviors.OfType<ElixirBehaviorTemplate>().FirstOrDefault();
                    if (elixirBehavior is not null && TryEquipElixir(wizard, message.Item, wizItemTemplate, elixirBehavior)) {
                        return 0;
                    }
                }
                // If it couldn't be equipped (requirements, already 3 active, duplicate type, etc.), fall back to inventory.
            }

            // Check if the item is an emote or teleport effect
            var customEmote = behaviors.OfType<CustomEmoteBehaviorTemplate>().FirstOrDefault();
            if (customEmote is { m_bitFieldNumber: >= 0 }) {
                if (OwnsCustomEmote(wizard, customEmote)) {
                    return s_errorPurchase;
                }

                if (customEmote.m_emoteType == CustomEmoteType.CE_Teleport) {
                    if(wizard.CountTeleportEffectsUnlocked() >= s_maxTeleportEffects) {
                        return s_errorMaxTeleportEffects;
                    }

                    transaction.OnCommit(() => wizard.UnlockCustomTeleportEffect(customEmote.m_bitFieldNumber));
                }
                else if(customEmote.m_emoteType == CustomEmoteType.CE_Emote) {
                    if (wizard.CountEmotesUnlocked() >= s_maxEmotes) {
                        return s_errorMaxEmotes;
                    }

                    transaction.OnCommit(() => wizard.UnlockCustomEmote(customEmote.m_bitFieldNumber));
                }else {
                    Logger.Error("Unknown CustomEmoteType: {0}", Logger.Args(customEmote.m_emoteType));
                }
            }
        }

        for (var i = 0; i < message.Count; i++) {
            if (!TryAddItemToInventory(transaction, wizard, message.Item, message.Texture, message.Decal)) {
                return s_errorInvFull;
            }
        }

        return 0;
    }

    private static bool OwnsCustomEmote(Wizard wizard, CustomEmoteBehaviorTemplate emote) {
        var stats = wizard.GameStats;
        return emote.m_emoteType == CustomEmoteType.CE_Teleport
            ? CustomEmoteOwnership.Owns(emote, stats.m_purchasedCustomTeleportEffects1, stats.m_purchasedCustomTeleportEffects2, stats.m_purchasedCustomTeleportEffects3)
            : CustomEmoteOwnership.Owns(emote, stats.m_purchasedCustomEmotes1, stats.m_purchasedCustomEmotes2, stats.m_purchasedCustomEmotes3);
    }

    private bool MeetsEquipRequirements(Wizard wizard, WizItemTemplate itemTemplate) {
        if (itemTemplate.m_equipRequirements is null) {
            return true;
        }

        var context = new GenericRequirementContext(
            itemTemplate.m_equipRequirements,
            SessionActor.ActorRef,
            wizard.GetInitializedGameObject(),
            wizard);

        return RequirementDispatcher.EvaluateRequirements(itemTemplate.m_equipRequirements, context);
    }

    private int TryDeliverGift(Wizard sender, WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_REQUEST message) {
        // Decision: any character may receive a gift, not only the sender's friends; the sender pays for it.
        var targetCharacter = WizardCollection.GetCharacterUnloaded(message.Recipient);
        if (targetCharacter is null && Wizard.TryGetCharacterId(message.Recipient, out var resolvedCharId)) {
            targetCharacter = WizardCollection.GetCharacterUnloaded(resolvedCharId);
        }

        if (targetCharacter is null) {
            Logger.Error("Recipient with CharacterId {0} was not found.", Logger.Args(message.Recipient));
            return s_errorFailedToSendGift;
        }

        var recipientAccountId = targetCharacter.AccountId;
        var targetAccount = AccountCollection.GetAccount(recipientAccountId);
        if (targetAccount is null) {
            Logger.Error("Account {0} for character was not found.", Logger.Args(recipientAccountId));
            return s_errorFailedToSendGift;
        }

        var giftRedemption = new CrownShopGiftRedemption {
            m_giverAccountID = sender.AccountId,
            m_itemCount = message.Count,
            m_itemId = message.Item,
            m_itemTransactionId = Guid.NewGuid().ToString("N")
        };

        if (!s_versionableSerializer.Serialize(giftRedemption, GiftPropertyFlags, out var serializedItem)) {
            Logger.Error("Failed to serialize CrownShopGiftRedemption.");
            return s_errorFailedToSendGift;
        }

        var mail = new Mail {
            m_recipientId = recipientAccountId,
            m_senderId = (GID) sender.CharId,
            m_messageData = Encoding.Latin1.GetString((byte[]) serializedItem),
            m_expireDuration = 0,
            m_messageType = 2,
            m_timeStamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            m_mailId = GenerateRandomUlong()
        };

        targetAccount.AddMail(mail);

        var itemTemplateId = (uint) message.Item;
        var removedFromWishlist = WishlistData.TryRemoveItem(targetCharacter.CrownShopWishlist, itemTemplateId, out var updatedWishlist);
        var removedItemStr = itemTemplateId.ToString();
        if (removedFromWishlist) {
            targetCharacter.CrownShopWishlist = updatedWishlist;
            WizardCollection.UpdateCharacterCSWishlist(targetCharacter);

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST {
                GlobalID = targetCharacter.GameObjectID,
                Data = updatedWishlist,
                WLItemsRemAftPur = removedItemStr
            });
            SendBuddyWishlistUpdate(SessionActor.ActorRef, targetCharacter, updatedWishlist);
        }

        // Notify the recipient if they are online
        if (TryGetOnlinePlayer(targetCharacter.CharId, out var onlinePlayer)) {
            var recipientActor = Context.ActorSelection(onlinePlayer.ActorPath);
            recipientActor.Tell(new SERVICE_101_PROTOCOL.MSG_DELIVER_GIFT {
                Mail = mail
            });

            if (removedFromWishlist) {
                recipientActor.Tell(new CHARACTER_103_PROTOCOL.MSG_UPDATE_WISHLIST {
                    CharId = targetCharacter.CharId,
                    WishlistData = updatedWishlist,
                    RemovedTemplateId = itemTemplateId
                });
            }
        }
        else if (removedFromWishlist) {
            NotifyBuddiesOfWishlistUpdate(targetCharacter, updatedWishlist, removedItemStr);
        }

        return 0;
    }

    private bool TryAddItemToInventory(GrantTransaction transaction, Wizard wizard, ulong itemTemplateId, int texture, int decal) {
        if (CoreObjectFactory.FinalizeCoreObject(itemTemplateId) is not WizClientObjectItem item) {
            Logger.Warning("Cannot add template {0} to inventory because it is not an item.", Logger.Args(itemTemplateId));
            return false;
        }

        item.m_primaryColor = texture;
        item.m_secondaryColor = decal;
        if (!wizard.AddItemToInventory(item)) {
            Logger.Warning("Could not add item {0} to inventory.", Logger.Args(itemTemplateId));
            return false;
        }

        transaction.OnRollback(() => wizard.RemoveItemFromInventory(item.m_globalID));

        if (!s_coreObjectSerializer.Serialize(item, 24, out var serializedItem)) {
            Logger.Warning("Failed to serialize core object {0}.", Logger.Args(itemTemplateId));
            return false;
        }

        transaction.OnCommit(() => {
            SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                GlobalID = wizard.GameObjectID,
                SerializedItem = serializedItem
            });

            // Adds the item to 'REVIEW MY ORDER'
            SendToSocket(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
                ItemGlobalID = item.m_globalID,
                ItemLocation = 1,
                ItemTemplateID = (uint) itemTemplateId
            });
        });

        return true;
    }

    private bool TryEquipElixir(Wizard wizard,
                                ulong templateId,
                                WizItemTemplate itemTemplate,
                                ElixirBehaviorTemplate elixirTemplate) {
        var durationSeconds = 0u;
        if (!string.IsNullOrEmpty(elixirTemplate.m_expireTime)) {
            _ = uint.TryParse(elixirTemplate.m_expireTime.ToString(), out durationSeconds);
        }

        // Case 1: Instant refill (duration == 0).
        if (durationSeconds == 0) {
            if (elixirTemplate.m_equipActionList?.m_results?.Count > 0) {
                ResultDispatcher.ExecuteResults(
                    Context,
                    elixirTemplate.m_equipActionList,
                    SessionActor.ActorRef,
                    wizard.GetInitializedGameObject());
            }

            PlaySound(elixirTemplate.m_equipSound);
            return true;
        }

        // Case 2: Timed elixir (duration > 0).
        var equipBehavior = wizard.EquipmentBehavior;
        if (!equipBehavior.CanEquipElixir(elixirTemplate)) {
            return false; // Can't equip right now; caller falls back to inventory.
        }

        var elixirItem = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(templateId);
        elixirItem.m_characterId = (GID) wizard.CharId;
        CoreObjectFactory.InitializeCoreObjectBehaviors(elixirItem, templateId);

        if (CoreObjectFactory.FindBehaviorInstance<ClientElixirBehavior>(elixirItem, out var clientElixir)) {
            clientElixir.m_expireTime = durationSeconds;
            clientElixir.m_statsApplied = true;
        }

        if (!equipBehavior.EquipElixir(elixirItem, durationSeconds)) {
            return false;
        }

        var addedEffects = CharacterEffectHelper.AddEffectsToWizard(wizard, itemTemplate);

        WizardItemCollection.AddItem(elixirItem);
        WizardCollection.UpdateCharacterItems(wizard);

        if (s_coreObjectSerializer.Serialize(elixirItem, 24, out var serializedItem)) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                GlobalID = wizard.GameObjectID,
                SerializedItem = serializedItem
            });
        }

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_ITEMACQUISITION {
            ItemGlobalID = elixirItem.m_globalID,
            ItemLocation = 1,
            ItemTemplateID = (uint) templateId
        });

        SendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM {
            ItemID = elixirItem.m_globalID,
            SlotName = "Elixir",
            IsEquip = 1
        });

        SendAddEffects(wizard, addedEffects);

        // Start HUD countdown timer and enable HUD icon.
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_SETELIXIRTIMER {
            GlobalID = elixirItem.m_globalID,
            TimerTime = durationSeconds
        });

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ELIXIRSTATECHANGE {
            parentID = elixirItem.m_globalID,
            EffectEnabled = 1
        });

        PlaySound(elixirTemplate.m_equipSound);

        // Update health and mana display with new maximums.
        var clientStats = wizard.GameStats.GetClientTypeAlternative();
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
            CharacterID = wizard.GameObjectID,
            NewHealth = wizard.GameStats.m_currentHitpoints,
            NewHealthMax = clientStats.m_baseHitpoints,
            DisplayDiff = 1
        });
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
            Mana = wizard.GameStats.m_currentMana,
            MaxMana = clientStats.m_baseMana,
            DisplayDiff = 1
        });

        return true;
    }

    private void SendAddEffects(Wizard wizard, List<GameEffectBase> effects) {
        if (effects is not { Count: > 0 }) {
            return;
        }

        foreach (var effect in effects) {
            if (!s_coreObjectSerializer.Serialize(effect, PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit, out var effectData)) {
                continue;
            }

            SendToSocket(new GAME_5_PROTOCOL.MSG_ADDEFFECT {
                GameObjectID = wizard.GameObjectID,
                EffectData = effectData
            });
        }
    }

    private void SendPurchaseError(int errorCode, ulong item, bool isInDuel) {
        Logger.Warning("Refused purchase of item {0}: {1}, in duel: {2}.",
            Logger.Args(item, s_errorNames.GetValueOrDefault(errorCode, errorCode.ToString()), isInDuel));
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_PCS_PURCHASE_RESPONSE {
            Error = errorCode
        });
    }

    private void SyncCrownBalance(Wizard wizard)
        => SendToSocket(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
            Failure = 0,
            TotalCrowns = wizard.Account.Crowns,
            CharacterID = wizard.CharId,
            CacheBalanceForCSSegmentation = 1
        });

    private void SyncGoldBalance(Wizard wizard)
        => SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch
        });

    // This message is fired when the PCS UI is closed.
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST))]
    private void ReceiveWishlistUpdate(WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST message) {
        if (!CrownShopHandler.TryAcceptWishlist(message.Data, out var wishlistData)) {
            Logger.Warning("Rejected crown shop wishlist: undecodable, over the size limit, or naming items the shop does not sell.");
            return;
        }

        var wizard = GetActiveWizard();
        if (wizard.CrownShopWishlist == wishlistData) {
            return;
        }

        var removedItems = CrownShopWishlist.RemovedItems(wizard.CrownShopWishlist, wishlistData);
        ApplyLocalWishlistUpdate(wizard, wishlistData, removedItems, sendToSelf: false);

        Logger.Information("Saved updated crown shop wishlist data for character {Id}",
            Logger.Args(wizard.CharId));
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_UPDATE_WISHLIST))]
    private void ReceiveWishlistUpdateInternal(CHARACTER_103_PROTOCOL.MSG_UPDATE_WISHLIST message) {
        var wizard = GetActiveWizard();
        if (wizard is null || wizard.CharId != message.CharId) {
            return;
        }

        ApplyLocalWishlistUpdate(wizard, message.WishlistData, message.RemovedTemplateId.ToString(), sendToSelf: true);
    }

    private void ApplyLocalWishlistUpdate(Wizard wizard, ByteString wishlistData, string removedItems, bool sendToSelf) {
        wizard.CrownShopWishlist = wishlistData;
        WizardCollection.UpdateCharacterCSWishlist(wizard);

        if (wizard.GameObject is not null) {
            if (CoreObjectFactory.FindBehaviorInstance<WishlistBehavior>(wizard.GameObject, out var wishlistBehavior)) {
                wishlistBehavior.m_wishlistData = wishlistData;
            }
            else {
                wizard.GameObject.m_inactiveBehaviors.Add(new WishlistBehavior {
                    m_behaviorTemplateNameID = 0x174F46F4,
                    m_wishlistData = wishlistData
                });
            }
        }

        var updateMsg = new WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST {
            GlobalID = wizard.GameObjectID,
            Data = wishlistData,
            WLItemsRemAftPur = removedItems ?? string.Empty
        };

        if (sendToSelf) {
            SendToSocket(updateMsg);
        }

        ZoneBroadcast(updateMsg, isSelfless: true, targets: ZoneBroadcastTarget.Players);

        // We want to notify all players, that our wishlist got updated
        NotifyBuddiesOfWishlistUpdate(wizard, wishlistData, removedItems);
    }

    private void NotifyBuddiesOfWishlistUpdate(Wizard owner, ByteString wishlistData, string removedItems) {
        var updateMsg = new WIZARD_12_PROTOCOL.MSG_PCS_UPDATEUSERWISHLIST {
            GlobalID = owner.GameObjectID,
            Data = wishlistData,
            WLItemsRemAftPur = removedItems ?? string.Empty
        };

        var buddies = BuddyRelationshipCollection.GetBuddiesForWizard(owner.CharId);
        foreach (var buddy in buddies.Where(b => b is not null)) {
            // only if they're online
            if (!TryGetOnlinePlayer(buddy.CharId, out var onlineBuddy)) {
                continue;
            }

            var buddyActor = Context.ActorSelection(onlineBuddy.ActorPath);
            buddyActor.Tell(updateMsg);
            SendBuddyWishlistUpdate(buddyActor, owner, wishlistData);
        }
    }

    private static void SendBuddyWishlistUpdate(ICanTell targetActor, Wizard owner, ByteString wishlistData) {
        var hasEquippedPet = owner.EquipmentBehavior?.SlotList?.Any(
            s => s.SlotType == EquipmentSlotType.Pet && s.ItemId != 0) == true;
        var showPvpOption = owner.GameStats?.m_showPVPOption is > 0 and var opt ? opt : 1u;

        targetActor.Tell(new GAME_5_PROTOCOL.MSG_BUDDYSTATS {
            BuddyID = owner.GameObjectID,
            Level = (uint) (owner.MagicSchoolBehavior?.Level ?? 1),
            School = (uint) (owner.MagicSchoolBehavior?.MagicSchool ?? 0),
            Gender = (sbyte) (owner.WizardAvatar?.m_eGender ?? 0),
            ShowPVPOption = showPvpOption,
            PetStatBlockCRC = hasEquippedPet ? 1u : 0u,
            WishlistBlock = wishlistData.IsEmpty ? WishlistData.Empty : wishlistData,
            WishlistBlockCRC = 1
        }, ActorRefs.NoSender);
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_CrownShopLogging))]
    private void ReceiveCrownShopLogging(WIZARD2_53_PROTOCOL.MSG_CrownShopLogging message) {
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_CrownShopLogging {
            Enabled = 0
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PCS_PATCH))]
    private void ReceivePCsPatch(WIZARD_12_PROTOCOL.MSG_PCS_PATCH message) {
        Logger.Warning("What is MSG_PCS_PATCH for? This is called when someone opens player gifts");
    }
}
