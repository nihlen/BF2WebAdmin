using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data.Repositories;

/// <summary>Map mods on EF Core (SQLite, SQL Server, Postgres), one short context per call.</summary>
public class MapRepository : IMapRepository
{
    private readonly IDbContextFactory<BF2Context> _contextFactory;

    public MapRepository(IDbContextFactory<BF2Context> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<MapMod>> GetAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MapMods.ToListAsync();
    }

    public async Task<MapMod> GetAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MapMods
            .Include(mm => mm.MapModObjects)
            .FirstOrDefaultAsync(mm => mm.Id == id);
    }

    public async Task CreateAsync(MapMod map)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.MapMods.AddAsync(map);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(MapMod map)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.MapMods.Update(map);
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<MapModSummary>> GetSummariesAsync(string map)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.MapMods.AsNoTracking();
        if (map != null)
            query = query.Where(mm => mm.Map == map);
        var list = await query
            .Select(mm => new MapModSummary(mm.Id, mm.Name, mm.Map, mm.CreatedBy, mm.OwnerId, mm.CreatedDate, mm.EditedDate, mm.MapModObjects.Count))
            .ToListAsync();
        return list.OrderBy(l => l.Map).ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<MapMod> FindByNameAsync(string map, string name)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lower = name.ToLower();
        return await context.MapMods
            .Include(mm => mm.MapModObjects)
            .FirstOrDefaultAsync(mm => mm.Map == map && mm.Name.ToLower() == lower);
    }

    public async Task<int> CountByOwnerAsync(string ownerId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.MapMods.CountAsync(mm => mm.OwnerId == ownerId);
    }

    public async Task ReplaceAsync(MapMod layout)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var stored = await context.MapMods.Include(mm => mm.MapModObjects).FirstOrDefaultAsync(mm => mm.Id == layout.Id)
                     ?? throw new InvalidOperationException($"No layout {layout.Id}");
        stored.Name = layout.Name;
        stored.CreatedBy = layout.CreatedBy;
        stored.OwnerId = layout.OwnerId;
        stored.EditedDate = layout.EditedDate;
        context.MapModObjects.RemoveRange(stored.MapModObjects);
        await context.SaveChangesAsync();

        foreach (var o in layout.MapModObjects)
        {
            o.MapModId = stored.Id;
            o.MapMod = null;
        }
        await context.MapModObjects.AddRangeAsync(layout.MapModObjects);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // With its objects: SQL Server's foreign key does not cascade
        var map = await context.MapMods.Include(mm => mm.MapModObjects).FirstOrDefaultAsync(mm => mm.Id == id);
        if (map == null)
            return;

        context.MapMods.Remove(map);
        await context.SaveChangesAsync();
    }
}