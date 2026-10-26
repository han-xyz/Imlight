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
 */

using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Collections;

public static class AdminAuditCollection {

    public const string CollectionName = "AdminAudit";
    private static readonly IDocumentStore s_store;

    static AdminAuditCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds an entry to the admin audit log.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    public static void AddEntry(AdminAuditEntry entry) {
        using var session = s_store.OpenSession();

        session.Store(entry);
        var metadata = session.Advanced.GetMetadataFor(entry);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

}
