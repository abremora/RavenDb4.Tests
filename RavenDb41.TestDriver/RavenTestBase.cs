using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.ServerWide.Operations;
using Raven.Embedded;
using System;

namespace RavenDb41.TestDriver {
    public class RavenTestBase {
        public static void SetupStore(Action<IDocumentStore> action) {
            IDocumentStore store = null;
            var database = Guid.NewGuid().ToString();

            try {
                store = GetEmbeddedStore(database);

                action(store);
            }
            finally {
                store?.Maintenance.Server.Send(new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters() {
                    DatabaseNames = new[] { database },
                    HardDelete = true
                }));
                store?.Dispose();
            }
        }

        public static IDocumentStore GetEmbeddedStore(string database) {
            var embedded = EmbeddedServer.Instance;
            var serverOptions = new ServerOptions { };
            embedded.StartServer(serverOptions);
            
            var options = new DatabaseOptions(database) {
                Conventions = new DocumentConventions {                   
                    FindCollectionName = t => t.Name,
                    UseOptimisticConcurrency = false
                }
            };

            return embedded.GetDocumentStore(options);
        }
    }
}
