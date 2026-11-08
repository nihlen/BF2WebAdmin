using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

/// <summary>dbo.JoinHistory, the BF2v2 site's player join table.</summary>
public interface IJoinHistoryRepository
{
    Task InsertAsync(PlayerJoin join);
}
