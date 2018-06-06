using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit.Testing;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Shouldly;
using Xunit;

namespace MassTransit.RavenDbIntegration.Tests
{
    public class Blah
    {
        public Guid CorrelationId { get; set; }
    }
    
    public class LocatingAnExistingSaga : IClassFixture<SagaPersistenceFixture>
    {
        private readonly SagaPersistenceFixture _fixture;
        readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

        public LocatingAnExistingSaga(SagaPersistenceFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task A_correlated_message_should_find_the_correct_saga()
        {
            Guid sagaId = NewId.NextGuid();
            var message = new InitiateSimpleSaga(sagaId);

            await _fixture.Harness.InputQueueSendEndpoint.Send(message);

            _fixture.Saga.Consumed.Select<InitiateSimpleSaga>().Any();

            Guid? found = await _fixture.SagaRepository.ShouldContainSaga(message.CorrelationId, TestTimeout);

            found.ShouldBe(sagaId);

            var nextMessage = new CompleteSimpleSaga {CorrelationId = sagaId};

            await _fixture.Harness.InputQueueSendEndpoint.Send(nextMessage);

            _fixture.Saga.Consumed.Select<CompleteSimpleSaga>().Any();
            await Task.Delay(TimeSpan.FromSeconds(1));

            found = await _fixture.SagaRepository.ShouldContainSaga(x => x.SomeOtherId == sagaId && x.Completed,
                TestTimeout);
            found.ShouldBe(sagaId);
        }

        [Fact]
        public async Task An_observed_message_should_find_and_update_the_correct_saga()
        {
            Guid sagaId = NewId.NextGuid();
            var message = new InitiateSimpleSaga(sagaId) {Name = "MySimpleSaga"};

            await _fixture.Harness.InputQueueSendEndpoint.Send(message);

            Guid? found = await _fixture.SagaRepository.ShouldContainSaga(message.CorrelationId, TestTimeout);

            found.ShouldBe(sagaId);

            var nextMessage = new ObservableSagaMessage("MySimpleSaga");

            await _fixture.Harness.InputQueueSendEndpoint.Send(nextMessage);

            found = await _fixture.SagaRepository.ShouldContainSaga(x => x.SomeOtherId == sagaId && x.Observed,
                TestTimeout);
            found.ShouldBe(sagaId);
        }

        [Fact]
        public async Task An_initiating_message_should_start_the_saga()
        {
            var sagaId = NewId.NextGuid();
            var message = new InitiateSimpleSaga(sagaId);

            await _fixture.Harness.InputQueueSendEndpoint.Send(message);

            var found = await _fixture.SagaRepository.ShouldContainSaga(message.CorrelationId, TestTimeout);

            found.ShouldBe(sagaId);
        }

    }

    public class SagaPersistenceFixture : IAsyncLifetime
    {
        private const string dbName = "MassTransitTest";
        private IDocumentStore Store { get; }
        public RavenDbSagaRepository<SimpleSaga> SagaRepository { get; }
        public InMemoryTestHarness Harness { get; }
        public SagaTestHarness<SimpleSaga> Saga { get; }

        public SagaPersistenceFixture()
        {
            Store = new DocumentStore()
            {
                Urls = new[] {"http://localhost:8080"},
                Database = dbName
            };
            Store.Initialize();
            
            SagaRepository = new RavenDbSagaRepository<SimpleSaga>(Store);
            Harness = new InMemoryTestHarness();
            Saga = Harness.Saga(SagaRepository);
        }

        public async Task InitializeAsync()
        {
            await Store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(dbName)));
            await Harness.Start();
        }

        public async Task DisposeAsync()
        {
            await Store.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(dbName, true));
            await Harness.Stop();
            Store.Dispose();
        }
    }
}