using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Exceptions;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using Dapper;
using Dapper.Contrib.Extensions;

namespace BF2WebAdmin.Data.Repositories;

public class SqlMapRepository : BaseSqlRepository<MapMod>, IMapRepository
{
    public SqlMapRepository(string connectionString) : base(connectionString) { }

    public new async Task<IEnumerable<MapMod>> GetAsync()
    {
        using (var conn = NewConnection)
        {
            return await conn.GetAllAsync<MapMod>();
        }
    }

    public new async Task<MapMod> GetAsync(Guid id)
    {
        using (var connection = NewConnection)
        {
            var map = await connection.GetAsync<MapMod>(id);
            //var predicate = Predicates.Field<MapModObject>(f => f.MapModId, Operator.Eq, map.Id);
            //map.Objects = conn.GetList<MapModObject>(predicate);
            map.MapModObjects = connection.Query<MapModObject>(
                @"SELECT [Id], [MapModId], [TemplateName], [Position], [Rotation] FROM [MapModObject] WHERE [MapModId] = @MapModId",
                new { MapModId = map.Id }
            ).ToList();
            return map;
        }
    }

    //public MapMod Get(string name)
    //{
    //    using (var conn = Connection)
    //    {
    //        var mapPredicate = Predicates.Field<MapMod>(f => f.Name, Operator.Eq, name);
    //        var map = conn.Get<MapMod>(mapPredicate);
    //        var objectPredicate = Predicates.Field<MapModObject>(f => f.MapModId, Operator.Eq, map.Id);
    //        map.Objects = conn.GetList<MapModObject>(objectPredicate);
    //        return map;
    //    }
    //}

    public new async Task CreateAsync(MapMod map)
    {
        using (var connection = NewConnection)
        {
            await connection.InsertAsync(map);
            await connection.InsertAsync(map.MapModObjects);
        }
    }

    public new async Task UpdateAsync(MapMod map)
    {
        using (var connection = NewConnection)
        {
            await connection.UpdateAsync(map);
            await connection.UpdateAsync(map.MapModObjects);
        }
    }

    public new async Task DeleteAsync(Guid id)
    {
        using (var connection = NewConnection)
        {
            var map = await connection.GetAsync<MapMod>(id);
            if (map == null)
                throw new EntityNotFoundException(id.ToString());

            await connection.DeleteAsync(map.MapModObjects);
            await connection.DeleteAsync(map);
        }
    }
}