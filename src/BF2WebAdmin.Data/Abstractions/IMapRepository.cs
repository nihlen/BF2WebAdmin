using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Data.Abstractions;

public interface IMapRepository : IRepository<MapMod>
{
    /// <summary>The layouts of a map (all maps with null), without their objects, by name.</summary>
    Task<IReadOnlyList<MapModSummary>> GetSummariesAsync(string map);

    /// <summary>A map's layout by name, ignoring case, with its objects; null when there is none.</summary>
    Task<MapMod> FindByNameAsync(string map, string name);

    /// <summary>How many layouts an owner has (<see cref="MapMod.OwnerId"/>).</summary>
    Task<int> CountByOwnerAsync(string ownerId);

    /// <summary>
    /// Saves a layout's name, author, owner and edit time over the stored one with the same id, and replaces its objects
    /// with the given ones. The id stays.
    /// </summary>
    Task ReplaceAsync(MapMod layout);
}

/// <summary>A layout without its objects.</summary>
public record MapModSummary(Guid Id, string Name, string Map, string CreatedBy, string OwnerId, DateTime CreatedDate, DateTime EditedDate, int ObjectCount);
