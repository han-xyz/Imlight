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
 * CROWN SHOP WISHLIST
 * ========================================================================
 * 
 * PURPOSE:
 * The removed-items list sent with a wishlist update, built by the server
 * from the stored and the new wishlist instead of taken from the client.
 * 
 * USAGE EXAMPLE:
 * CrownShopService.ReceiveWishlistUpdate.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.Linq;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.CrownShop;

internal static class CrownShopWishlist {

    public static string RemovedItems(ByteString stored, ByteString updated) {
        if (!WishlistData.TryFromByteString(stored, out var before) || !WishlistData.TryFromByteString(updated, out var after)) {
            return string.Empty;
        }

        return string.Join(',', before.Items.Except(after.Items));
    }

}
