using BF2WebAdmin.DAL.Abstractions;
using BF2WebAdmin.DAL.Entities;
using Raven.Client;

namespace BF2WebAdmin.DAL.Repositories
{
    public class RavenScriptRepository : BaseRavenRepository<GameScript>, IScriptRepository
    {
        public RavenScriptRepository(IDocumentStore store) : base(store) { }
    }
}