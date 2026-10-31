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
 * DATABASE STORE TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for the database singleton's store: callers that ask for it at the same
 * moment, as the collections' static constructors do at boot, get one store.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Imlight.Common;
using Imlight.CoreLib.WizardData;
using Raven.Client.Documents;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class DatabaseStoreTests {

    [Fact]
    public void Store_AskedFromManyThreads_IsCreatedOnce() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, string.Join('\n',
                "[Database]",
                "DatabaseMaxNumberOfRequestsPerSession = 16",
                "DatabaseRequestTimeoutInSeconds = 90",
                "DatabaseWaitForNonStaleResultsTimeout = 5"));
            ConfigurationManager.Initialize(iniFilePath);
            var database = SlowDatabase.Instance;
            using var start = new Barrier(8);

            var tasks = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => {
                    start.SignalAndWait();
                    return database.Store;
                }))
                .ToArray();
            var stores = Task.WhenAll(tasks).GetAwaiter().GetResult();

            Assert.Equal(1, database.CreatedStores);
            Assert.All(stores, store => Assert.Same(stores[0], store));
        } finally {
            File.Delete(iniFilePath);
        }
    }

    private sealed class SlowDatabase : RavenDatabaseSingleton<SlowDatabase> {

        private int _createdStores;

        public int CreatedStores => _createdStores;

        protected override string DatabaseName => "Test";
        protected override string Url => "";
        protected override string CertificatePath => "";
        protected override string CertificateThumbprint => "";

        protected override IDocumentStore CreateEmbeddedStore() {
            Interlocked.Increment(ref _createdStores);
            Thread.Sleep(200);

            return new DocumentStore();
        }

    }

}
