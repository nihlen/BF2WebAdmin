using BF2WebAdmin.DAL.Abstractions;
using BF2WebAdmin.DAL.Entities;
using Raven.Client;

namespace BF2WebAdmin.DAL.Repositories
{
    public class RavenMapRepository : BaseRavenRepository<MapMod>, IMapRepository
    {
        public RavenMapRepository(IDocumentStore store) : base(store) { }
    }
}