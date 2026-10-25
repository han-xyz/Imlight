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
 * GRANT TRANSACTION TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for the reserve, grant, commit or roll back sequence that
 * crown shop purchases and gift redemptions run through.
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
using Imlight.CoreLib.Game.CrownShop;
using Xunit;

namespace Imlight.Tests;

public sealed class GrantTransactionTests {

    private const int OutOfSpace = 7;
    private const int NotEnoughCrowns = 9;

    private readonly List<string> _events = [];

    [Fact]
    public void Purchase_ReservationRefused_GrantsNothing() {
        var error = GrantTransaction.Run(
            reserve: () => NotEnoughCrowns,
            release: () => _events.Add("refund"),
            grant: _ => {
                _events.Add("grant");
                return 0;
            });

        Assert.Equal(NotEnoughCrowns, error);
        Assert.Empty(_events);
    }

    [Fact]
    public void Purchase_GrantFailsPartway_RemovesGrantedInReverseAndRefunds() {
        var error = GrantTransaction.Run(
            reserve: Reserve,
            release: () => _events.Add("refund"),
            grant: transaction => {
                GrantUnit(transaction, "item");
                GrantUnit(transaction, "reagent");
                return OutOfSpace;
            });

        Assert.Equal(OutOfSpace, error);
        Assert.Equal(["debit", "grant item", "grant reagent", "remove reagent", "remove item", "refund"], _events);
    }

    [Fact]
    public void Purchase_GrantThrows_RemovesGrantedAndRefundsThenRethrows() {
        Assert.Throws<InvalidCastException>(() => GrantTransaction.Run(
            reserve: Reserve,
            release: () => _events.Add("refund"),
            grant: transaction => {
                GrantUnit(transaction, "item");
                throw new InvalidCastException();
            }));

        Assert.Equal(["debit", "grant item", "remove item", "refund"], _events);
    }

    [Fact]
    public void Purchase_AllGranted_KeepsDebitAndNotifiesAfterwards() {
        var error = GrantTransaction.Run(
            reserve: Reserve,
            release: () => _events.Add("refund"),
            grant: transaction => {
                GrantUnit(transaction, "item");
                transaction.OnCommit(() => _events.Add("notify item"));
                return 0;
            });

        Assert.Equal(0, error);
        Assert.Equal(["debit", "grant item", "notify item"], _events);
    }

    [Fact]
    public void GiftRedemption_GrantFails_PutsTheMailBackAndSendsNothing() {
        var error = GrantTransaction.Run(
            reserve: () => {
                _events.Add("take mail");
                return 0;
            },
            release: () => _events.Add("restore mail"),
            grant: transaction => {
                GrantUnit(transaction, "item");
                transaction.OnCommit(() => _events.Add("notify item"));
                return OutOfSpace;
            });

        Assert.Equal(OutOfSpace, error);
        Assert.Equal(["take mail", "grant item", "remove item", "restore mail"], _events);
    }

    private int Reserve() {
        _events.Add("debit");
        return 0;
    }

    private void GrantUnit(GrantTransaction transaction, string what) {
        _events.Add($"grant {what}");
        transaction.OnRollback(() => _events.Add($"remove {what}"));
    }

}
