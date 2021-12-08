using System.Threading.Tasks;
using MassTransit.Saga;
using Raven.Client.Documents;

namespace MassTransit.RavenDbIntegration
{
    public static class RavenDbSagaConventions
    {
        public static Task<string> GetSagaDocumentId(string dbName, ISaga saga)
        {
            return Task.FromResult($"{typeof (ISaga).Name}/{saga.CorrelationId}");
        }

        public static void RegisterSagaIdConvention(this IDocumentStore store)
        {
            store.Conventions.RegisterAsyncIdConvention<ISaga>(GetSagaDocumentId);
        }
    }
}