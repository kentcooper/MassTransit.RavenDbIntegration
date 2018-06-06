using System;
using MassTransit.Saga;

namespace MassTransit.RavenDbIntegration
{
    public static class RavenDbSagaConventions
    {
        public static string GetSagaDocumentId(ISaga saga) =>
            $"{saga.GetType().Name}/{saga.CorrelationId}";

        public static string GetSagaDocumentId<T>(Guid correlationId) where T : ISaga =>
            $"{typeof(T).Name}/{correlationId}";
    }
}