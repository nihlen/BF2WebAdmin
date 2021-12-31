using System;
using Raven.Client;
using Raven.Client.Document;

namespace BF2WebAdmin.Data
{
    public static class DocumentStoreHolder
    {
        private static readonly Lazy<IDocumentStore> DocumentStore = new Lazy<IDocumentStore>(CreateStore);

        public static IDocumentStore Store => DocumentStore.Value;

        private static IDocumentStore CreateStore()
        {
            var store = new DocumentStore
            {
                Url = "http://localhost:8080",
                DefaultDatabase = "BF2v2"
            }.Initialize();

            return store;
        }
    }
}