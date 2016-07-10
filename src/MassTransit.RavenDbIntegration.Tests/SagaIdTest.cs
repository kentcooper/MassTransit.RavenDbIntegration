using System;
using System.Threading.Tasks;
using MassTransit.Saga;
using NUnit.Framework;
using Raven.Client;
using Raven.Client.Embedded;

namespace MassTransit.RavenDbIntegration.Tests
{
    [TestFixture]
    public class SagaIdTest
    {
        private IDocumentStore _store;

        [OneTimeSetUp]
        public void Setup()
        {
            _store = new EmbeddableDocumentStore
            {
                RunInMemory = true
            };
            _store.Initialize();
            _store.RegisterSagaIdConvention();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _store.Dispose();
        }

        [Test]
        public async Task TestCorrelationId()
        {
            var guid = Guid.NewGuid();
            using (var session = _store.OpenAsyncSession())
            {
                var testSaga = new TestSaga() {CorrelationId = guid};
                var sagaDocId = session.Advanced.DocumentStore.Conventions.FindFullDocumentKeyFromNonStringIdentifier(guid, typeof(TestSaga), false);
                await session.StoreAsync(testSaga, sagaDocId);
                await session.SaveChangesAsync();
            }

            using (var session = _store.OpenAsyncSession())
            {
                var sagaDocId = session.Advanced.DocumentStore.Conventions.FindFullDocumentKeyFromNonStringIdentifier(guid, typeof(TestSaga), false);

                var test = await session.LoadAsync<TestSaga>(sagaDocId); //$"{typeof (ISaga).Name}/{guid}");
                Assert.AreEqual(guid, test.CorrelationId);

            }
        }

    }
}