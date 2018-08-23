using Raven.Client.ServerWide.Operations;
using Raven.Embedded;
using System;
using Xunit;

namespace RavenDb41.TestDriver {
    public class TestDriverTests {
        // FAILS!
        // System.IO.InvalidDataException: The database '6b665eac-d0f9-45cf-887f-88b03988b6d6' 
        // was created but is not accessible, because all of the nodes on which this database 
        // was supposed to reside on, threw an exception. 
        // ---> System.AggregateException: One or more errors occurred. 
        // 
        // Note: First run ok. Second run fails. Deleting folder in 
        // bin\Debug\netcoreapp2.1\RavenDB\* will solve the problem for the first run again
        [Fact]
        public void GetEmbeddedDocumentStore_CreateMultipleDatabases_Returns() {
            var database = Guid.NewGuid().ToString();
      
            EmbeddedServer.Instance.StartServer();

            using (var store = EmbeddedServer.Instance.GetDocumentStore(database)) {

                // Deleting DB does not help either
                store?.Maintenance.Server.Send(new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters() {
                    DatabaseNames = new[] { database },
                    HardDelete = true
                }));
            }
        }
    }
}
