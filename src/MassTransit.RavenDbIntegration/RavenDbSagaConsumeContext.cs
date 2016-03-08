using System;
using System.Threading.Tasks;
using MassTransit.Context;
using MassTransit.Logging;
using MassTransit.Saga;
using MassTransit.Util;
using Raven.Client;

namespace MassTransit.RavenDbIntegration
{
    public class RavenDbSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextProxyScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, ISaga
    {
        static readonly ILog _log = Logger.Get<RavenDbSagaRepository<TSaga>>();
        readonly IAsyncDocumentSession _session;

        public RavenDbSagaConsumeContext(IAsyncDocumentSession session, ConsumeContext<TMessage> context, TSaga instance)
            : base(context)
        {
            Saga = instance;
            _session = session;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        SagaConsumeContext<TSaga, T> SagaConsumeContext<TSaga>.PopContext<T>()
        {
            var context = this as SagaConsumeContext<TSaga, T>;
            if (context == null)
                throw new ContextException($"The ConsumeContext<{TypeMetadataCache<TMessage>.ShortName}> could not be cast to {TypeMetadataCache<T>.ShortName}");

            return context;
        }

        Task SagaConsumeContext<TSaga>.SetCompleted()
        {
            _session.Delete(Saga);
            IsCompleted = true;
            if (_log.IsDebugEnabled)
            {
                _log.DebugFormat("SAGA:{0}:{1} Removed {2}", TypeMetadataCache<TSaga>.ShortName, TypeMetadataCache<TMessage>.ShortName,
                    Saga.CorrelationId);
            }

            return TaskUtil.Completed;
        }

        public TSaga Saga { get; }
        public bool IsCompleted { get; private set; }
    }
}