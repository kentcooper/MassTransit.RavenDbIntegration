using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MassTransit.Saga;
using Newtonsoft.Json;

namespace MassTransit.RavenDbIntegration.Tests
{
    public class SimpleSaga :
        InitiatedBy<InitiateSimpleSaga>,
        Observes<ObservableSagaMessage, SimpleSaga>,
        Orchestrates<CompleteSimpleSaga>,
        ISaga
    {
        public bool Completed { get; private set; }
        public bool Initiated { get; private set; }
        public bool Observed { get; private set; }
        public string Name { get; private set; }
        
        public Guid SomeOtherId { get; set; }
        public Guid CorrelationId { get; set; }

        public async Task Consume(ConsumeContext<InitiateSimpleSaga> context)
        {
            Initiated = true;
            Name = context.Message.Name;

            SomeOtherId = CorrelationId;
        }

        public async Task Consume(ConsumeContext<ObservableSagaMessage> message)
        {
            Observed = true;
        }

        [JsonIgnore]
        Expression<Func<SimpleSaga, ObservableSagaMessage, bool>> Observes<ObservableSagaMessage, SimpleSaga>.CorrelationExpression
        {
            get { return (saga, message) => saga.Name == message.Name; }
        }

        public async Task Consume(ConsumeContext<CompleteSimpleSaga> message)
        {
            Completed = true;
        }

    }
}