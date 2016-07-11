using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MassTransit.Logging;
using MassTransit.Pipeline;
using MassTransit.Saga;
using MassTransit.Util;
using Raven.Client;
using Raven.Client.Linq;

namespace MassTransit.RavenDbIntegration
{
    public class RavenDbSagaRepository<TSaga> : ISagaRepository<TSaga>, IQuerySagaRepository<TSaga>, IFetchSagaRepository<TSaga>
        where TSaga : class, ISaga
    {
        private static readonly ILog _log = Logger.Get<RavenDbSagaRepository<TSaga>>();
        private readonly IDocumentStore _store;

        public RavenDbSagaRepository(IDocumentStore store)
        {
            _store = store;
        }

        public async Task<IEnumerable<Guid>> Find(ISagaQuery<TSaga> query)
        {
            using (var session = _store.OpenAsyncSession())
            {
                return await session.Query<TSaga>()
                    .Where(query.FilterExpression)
                    .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                    .Select(x => x.CorrelationId)
                    .ToListAsync();
            }
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateScope("sagaRepository");
            scope.Set(new
            {
                Persistence = "ravenDb"
            });
        }

        async Task ISagaRepository<TSaga>.Send<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next)
        {
            if (!context.CorrelationId.HasValue)
                throw new SagaException("The CorrelationId was not specified", typeof (TSaga), typeof (T));

            var sagaId = context.CorrelationId.Value;
            using (var session = _store.OpenAsyncSession())
            {
                var sagaDocId = session.Advanced.DocumentStore
                    .Conventions.FindFullDocumentKeyFromNonStringIdentifier(sagaId, typeof(TSaga), false);
                var inserted = false;
                TSaga instance;

                if (policy.PreInsertInstance(context, out instance))
                    inserted = await PreInsertSagaInstance<T>(session, instance, inserted);

                if (instance == null)
                    instance = await session.LoadAsync<TSaga>(sagaDocId);
                if (instance == null)
                {
                    var missingSagaPipe = new MissingPipe<T>(session, next);

                    await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
                }
                else
                {
                    if (_log.IsDebugEnabled)
                        _log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName,
                            instance.CorrelationId, TypeMetadataCache<T>.ShortName);

                    var sagaConsumeContext = new RavenDbSagaConsumeContext<TSaga, T>(session, context, instance);

                    await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);

                    if (inserted && !sagaConsumeContext.IsCompleted)
                    {
                        sagaDocId = session.Advanced.DocumentStore
                            .Conventions.FindFullDocumentKeyFromNonStringIdentifier(instance.CorrelationId, instance.GetType(), false);
                        await session.StoreAsync(instance, sagaDocId);
                    }
                }
                await session.SaveChangesAsync();
            }
        }

        public async Task SendQuery<T>(SagaQueryConsumeContext<TSaga, T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            using (var session = _store.OpenAsyncSession())
            {
                try
                {
                    var instances = await session.Query<TSaga>()
                        .Where(context.Query.FilterExpression)
                        .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                        .ToListAsync();

                    if (instances.Count == 0)
                    {
                        var missingSagaPipe = new MissingPipe<T>(session, next);
                        await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
                    }
                    else
                    {
                        await
                            Task.WhenAll(
                                instances.Select(instance => SendToInstance(context, policy, instance, next, session)))
                                .ConfigureAwait(false);
                    }
                    await session.SaveChangesAsync();
                }
                catch (SagaException sex)
                {
                    if (_log.IsErrorEnabled)
                        _log.Error("Saga Exception Occurred", sex);
                }
                catch (Exception ex)
                {
                    if (_log.IsErrorEnabled)
                        _log.Error($"SAGA:{TypeMetadataCache<TSaga>.ShortName} Exception {TypeMetadataCache<T>.ShortName}", ex);

                    throw new SagaException(ex.Message, typeof(TSaga), typeof(T), Guid.Empty, ex);
                }
            }
        }

        private static async Task<bool> PreInsertSagaInstance<T>(IAsyncDocumentSession session, TSaga instance,
            bool inserted)
        {
            try
            {
                var sagaDocId = session.Advanced.DocumentStore
                    .Conventions.FindFullDocumentKeyFromNonStringIdentifier(instance.CorrelationId, instance.GetType(), false);
                await session.StoreAsync(instance, sagaDocId);
                await session.SaveChangesAsync();

                inserted = true;

                _log.DebugFormat("SAGA:{0}:{1} Insert {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId,
                    TypeMetadataCache<T>.ShortName);
            }
            catch (Exception ex)
            {
                if (_log.IsDebugEnabled)
                {
                    _log.DebugFormat("SAGA:{0}:{1} Dupe {2} - {3}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName, ex.Message);
                }
            }
            return inserted;
        }

        private static async Task SendToInstance<T>(SagaQueryConsumeContext<TSaga, T> context,
            ISagaPolicy<TSaga, T> policy, TSaga instance,
            IPipe<SagaConsumeContext<TSaga, T>> next, IAsyncDocumentSession session)
            where T : class
        {
            try
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName);

                var sagaConsumeContext = new RavenDbSagaConsumeContext<TSaga, T>(session, context, instance);

                await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);
            }
            catch (SagaException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SagaException(ex.Message, typeof (TSaga), typeof (T), instance.CorrelationId, ex);
            }
        }

        /// <summary>
        ///     Once the message pipe has processed the saga instance, add it to the saga repository
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        private class MissingPipe<TMessage> :
            IPipe<SagaConsumeContext<TSaga, TMessage>>
            where TMessage : class
        {
            private readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;
            private readonly IAsyncDocumentSession _session;

            public MissingPipe(IAsyncDocumentSession session, IPipe<SagaConsumeContext<TSaga, TMessage>> next)
            {
                _session = session;
                _next = next;
            }

            void IProbeSite.Probe(ProbeContext context)
            {
                _next.Probe(context);
            }

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                if (_log.IsDebugEnabled)
                {
                    _log.DebugFormat("SAGA:{0}:{1} Added {2}", TypeMetadataCache<TSaga>.ShortName,
                        context.Saga.CorrelationId,
                        TypeMetadataCache<TMessage>.ShortName);
                }

                SagaConsumeContext<TSaga, TMessage> proxy = new RavenDbSagaConsumeContext<TSaga, TMessage>(_session,
                    context, context.Saga);

                await _next.Send(proxy).ConfigureAwait(false);

                if (!proxy.IsCompleted)
                {
                    var sagaDocId = _session.Advanced.DocumentStore
                        .Conventions.FindFullDocumentKeyFromNonStringIdentifier(context.Saga.CorrelationId, context.Saga.GetType(), false);
                    await _session.StoreAsync(context.Saga, sagaDocId);
                }
            }
        }

        public async Task<IList<TSaga>> FindWhere(Expression<Func<TSaga, bool>> filter)
        {
            using (var session = _store.OpenAsyncSession())
                return await session.Query<TSaga>().Where(filter).ToListAsync();
        }

        public async Task<TSaga> Load(Guid sagaId)
        {
            using (var session = _store.OpenAsyncSession())
                return await session.LoadAsync<TSaga>(sagaId);
        }
    }
}