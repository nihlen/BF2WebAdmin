using System;
using System.Collections.Generic;
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