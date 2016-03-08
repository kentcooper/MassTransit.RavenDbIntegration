using System;
using MassTransit.Saga;

namespace MassTransit.RavenDbIntegration.Tests
{
    public class TestSaga : ISaga
    {
        public Guid CorrelationId { get; set; }
    }
}