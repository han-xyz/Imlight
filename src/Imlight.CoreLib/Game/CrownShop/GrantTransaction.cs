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
 * GRANT TRANSACTION
 * ========================================================================
 * 
 * PURPOSE:
 * Takes the payment (or the gift mail) first, grants, and on any failure removes
 * what was granted and gives the payment back, so a purchase is all or nothing.
 * 
 * USAGE EXAMPLE:
 * CrownShopService.ReceivePurchaseRequest and GiftingService.ReceiveRedeemGift.
 * 
 * NOTE:
 * Client notifications go through OnCommit so a rolled-back grant never reaches the client.
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.CrownShop;

internal sealed class GrantTransaction {

    private readonly List<Action> _rollbacks = [];
    private readonly List<Action> _commits = [];

    private GrantTransaction() { }

    public void OnRollback(Action undo) => _rollbacks.Add(undo);

    public void OnCommit(Action notify) => _commits.Add(notify);

    /// <summary>
    /// Runs <paramref name="reserve"/>, then <paramref name="grant"/>. A non-zero error from <paramref name="grant"/>,
    /// or an exception, undoes the grants in reverse order and runs <paramref name="release"/>.
    /// </summary>
    /// <returns>0 on success, otherwise the error from <paramref name="reserve"/> or <paramref name="grant"/>.</returns>
    public static int Run(Func<int> reserve, Action release, Func<GrantTransaction, int> grant) {
        var reserveError = reserve();
        if (reserveError != 0) {
            return reserveError;
        }

        var transaction = new GrantTransaction();
        int grantError;
        try {
            grantError = grant(transaction);
        } catch {
            transaction.RollBack();
            release();
            throw;
        }

        if (grantError != 0) {
            transaction.RollBack();
            release();
            return grantError;
        }

        foreach (var notify in transaction._commits) {
            notify();
        }

        return 0;
    }

    private void RollBack() {
        for (var i = _rollbacks.Count - 1; i >= 0; i--) {
            _rollbacks[i]();
        }
    }

}
