using System;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Data;

/// <summary>The databases WebAdmin runs on (configuration Database:Provider, docs/database.md).</summary>
public enum DatabaseProvider
{
    /// <summary>EF Core with the SQLite migrations (<see cref="BF2Context"/>), for small installs.</summary>
    Sqlite,

    /// <summary>EF Core with the SQL Server migrations (<see cref="SqlServerBF2Context"/>).</summary>
    SqlServer,

    /// <summary>EF Core with the Postgres migrations (<see cref="PostgresBF2Context"/>).</summary>
    Postgres,
}

public static class DatabaseProviders
{
    /// <summary>
    /// The provider from the setting (sqlite, sqlserver or postgres, any case), or without one from the connection string:
    /// ".sqlite" in it is SQLite (the old rule), a "Host=" key is Postgres (Npgsql), anything else SQL Server.
    /// </summary>
    public static DatabaseProvider Resolve(string configured, string connectionString)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim().ToLowerInvariant() switch
            {
                "sqlite" => DatabaseProvider.Sqlite,
                "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
                "postgres" or "postgresql" or "npgsql" => DatabaseProvider.Postgres,
                _ => throw new ArgumentException($"Database:Provider '{configured}' is not one of sqlite, sqlserver, postgres"),
            };
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("ConnectionStrings:BF2DB is not set");

        if (connectionString.Contains(".sqlite", StringComparison.OrdinalIgnoreCase))
            return DatabaseProvider.Sqlite;

        if (IsNpgsqlConnectionString(connectionString))
            return DatabaseProvider.Postgres;

        return DatabaseProvider.SqlServer;
    }

    /// <summary>True when the connection string has a Host key (Npgsql's name for the server; SQL Server uses Server or Data Source).</summary>
    public static bool IsNpgsqlConnectionString(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var key = part.Split('=', 2)[0].Trim();
            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static DbContextOptions<PostgresBF2Context> PostgresOptions(string connectionString) =>
        new DbContextOptionsBuilder<PostgresBF2Context>().UseNpgsql(connectionString).Options;

    public static DbContextOptions<SqlServerBF2Context> SqlServerOptions(string connectionString) =>
        new DbContextOptionsBuilder<SqlServerBF2Context>().UseSqlServer(connectionString).Options;
}

/// <summary>
/// An <see cref="IDbContextFactory{BF2Context}"/> from a delegate. The EF repositories take a factory and use one short
/// context per call, so the module managers (which resolve them once, from their own service provider) neither share a
/// context between threads nor read stale tracked rows. Also lets <see cref="PostgresBF2Context"/> and
/// <see cref="SqlServerBF2Context"/> stand in for <see cref="BF2Context"/>.
/// </summary>
public sealed class DelegateContextFactory : IDbContextFactory<BF2Context>
{
    private readonly Func<BF2Context> _create;

    public DelegateContextFactory(Func<BF2Context> create)
    {
        _create = create;
    }

    public BF2Context CreateDbContext() => _create();
}
