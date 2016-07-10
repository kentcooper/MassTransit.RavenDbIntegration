using System;
using System.Threading.Tasks;
using MassTransit.Saga;
using MassTransit.TestFramework;
using NUnit.Framework;
using Raven.Client;
using Raven.Client.Embedded;
using Shouldly;

namespace MassTransit.RavenDbIntegration.Tests
{
    [TestFixture, Category("Integration")]
    public class LocatingAnExistingSaga : InMemoryTestFixture
    {
        private IDocumentStore _store;

        [OneTimeSetUp]
        public void SetupStore()
        {
        }

        [OneTimeTearDown]
        public void TearDownStore()
        {
            _store.Dispose();
        }

        [Test]
        public async Task A_correlated_message_should_find_the_correct_saga()
        {
            Guid sagaId = NewId.NextGuid();
            var message = new InitiateSimpleSaga(sagaId);

            await InputQueueSendEndpoint.Send(message);

            Guid? foundId = await _sagaRepository.Value.ShouldContainSaga(message.CorrelationId, TestTimeout);

            foundId.HasValue.ShouldBe(true);

            var nextMessage = new CompleteSimpleSaga { CorrelationId = sagaId };

            await InputQueueSendEndpoint.Send(nextMessage);

            foundId = await _sagaRepository.Value.ShouldContainSaga(x => x.CorrelationId == sagaId && x.Completed, TestTimeout);

            foundId.HasValue.ShouldBe(true);
        }

        [Test]
        public async Task An_initiating_message_should_start_the_saga()
        {
            Guid sagaId = NewId.NextGuid();
            var message = new InitiateSimpleSaga(sagaId);

            await InputQueueSendEndpoint.Send(message);

            Guid? foundId = await _sagaRepository.Value.ShouldContainSaga(message.CorrelationId, TestTimeout);

            foundId.HasValue.ShouldBe(true);
        }

        readonly Lazy<ISagaRepository<SimpleSaga>> _sagaRepository;

        public LocatingAnExistingSaga()
        {
            _store = new EmbeddableDocumentStore
            {
                RunInMemory = true
            };
            _store.Initialize();
            _store.RegisterSagaIdConvention();
            _sagaRepository = new Lazy<ISagaRepository<SimpleSaga>>(() => new RavenDbSagaRepository<SimpleSaga>(_store));
        }

        protected override void ConfigureInputQueueEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            configurator.Saga(_sagaRepository.Value);
        }
    }
}