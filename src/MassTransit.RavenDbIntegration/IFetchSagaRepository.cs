using System.Linq;
using MassTransit.Saga;
using System.Threading.Tasks;
using System;

namespace MassTransit.RavenDbIntegration
{
    public interface IFetchSagaRepository<TSaga> where TSaga: class, ISaga
    {
        IQueryable<TSaga> Where();
        Task<TSaga> Load(Guid sagaId);
    }
}