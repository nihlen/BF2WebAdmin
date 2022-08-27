using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Common.Exceptions;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;

namespace BF2WebAdmin.Data.Abstractions;

public abstract class BaseSqlRepository<T> : IRepository<T> where T : class
{
    private readonly string _connectionString;
    protected IDbConnection NewConnection => new SqlConnection(_connectionString);

    protected BaseSqlRepository(string connectionString)
    {
        _connectionString = connectionString;

        // Use singular table names by default
        SqlMapperExtensions.TableNameMapper = type => type.Name;

        // Map to string
        SqlMapper.AddTypeHandler(new PositionTypeHandler());
        SqlMapper.AddTypeHandler(new RotationTypeHandler());
    }

    public async Task<IEnumerable<T>> GetAsync()
    {
        using (var conn = NewConnection)
        {
            return await conn.GetAllAsync<T>();
        }
    }

    public async Task<T> GetAsync(Guid id)
    {
        using (var conn = NewConnection)
        {
            return await conn.GetAsync<T>(id);
        }
    }

    public async Task CreateAsync(T map)
    {
        using (var conn = NewConnection)
        {
            await conn.InsertAsync(map);
        }
    }

    public async Task UpdateAsync(T map)
    {
        using (var conn = NewConnection)
        {
            await conn.UpdateAsync(map);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        using (var conn = NewConnection)
        {
            var entity = await conn.GetAsync<T>(id);
            if (entity == null)
                throw new EntityNotFoundException(id.ToString());

            await conn.DeleteAsync(entity);
        }
    }
}

public class PositionTypeHandler : SqlMapper.TypeHandler<Position>
{
    public override void SetValue(IDbDataParameter parameter, Position value)
    {
        parameter.Value = value.ToString();
    }

    public override Position Parse(object value)
    {
        return Position.Parse((string)value);
    }
}

public class RotationTypeHandler : SqlMapper.TypeHandler<Rotation>
{
    public override void SetValue(IDbDataParameter parameter, Rotation value)
    {
        parameter.Value = value.ToString();
    }

    public override Rotation Parse(object value)
    {
        return Rotation.Parse((string)value);
    }
}