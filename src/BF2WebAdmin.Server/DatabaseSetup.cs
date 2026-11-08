using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Server;

/// <summary>
/// The database from the configuration: ConnectionStrings:BF2DB and Database:Provider (sqlite, sqlserver or postgres;
/// without it the connection string decides, see <see cref="DatabaseProviders.Resolve"/>). Used by Program.cs and by
/// every module manager, which builds its own service provider. docs/database.md describes the setup.
/// </summary>
public sealed record DatabaseSetup(DatabaseProvider Provider, string ConnectionString)
{
    public const string ProviderKey = "Database:Provider";

    public static DatabaseSetup FromConfiguration(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BF2DB");
        var provider = DatabaseProviders.Resolve(configuration[ProviderKey], connectionString);
        return new DatabaseSetup(provider, connectionString!);
    }

    /// <summary>
    /// The repositories: EF Core on SQLite and Postgres (scoped, each call on its own short context from a factory), Dapper
    /// on SQL Server (singletons). <paramref name="maps"/> adds <see cref="IMapRepository"/>, <paramref name="joins"/>
    /// <see cref="IJoinHistoryRepository"/>.
    /// </summary>
    public void AddRepositories(IServiceCollection services, bool maps, bool joins)
    {
        switch (Provider)
        {
            case DatabaseProvider.Sqlite:
            case DatabaseProvider.Postgres:
                AddContextFactory(services);
                services.AddScoped<IServerSettingsRepository, ServerSettingsRepository>();
                services.AddScoped<IMatchRepository, MatchRepository>();
                if (maps)
                    services.AddScoped<IMapRepository, MapRepository>();
                if (joins)
                    services.AddScoped<IJoinHistoryRepository, JoinHistoryRepository>();
                break;

            case DatabaseProvider.SqlServer:
                services.AddSingleton<IServerSettingsRepository>(_ => new SqlServerSettingsRepository(ConnectionString));
                services.AddSingleton<IMatchRepository>(_ => new SqlMatchRepository(ConnectionString));
                if (maps)
                    services.AddSingleton<IMapRepository>(_ => new SqlMapRepository(ConnectionString));
                if (joins)
                    services.AddSingleton<IJoinHistoryRepository>(_ => new SqlJoinHistoryRepository(ConnectionString));
                break;
        }
    }

    private void AddContextFactory(IServiceCollection services)
    {
        if (Provider == DatabaseProvider.Sqlite)
        {
            services.AddDbContextFactory<BF2Context>(o => o.UseSqlite(ConnectionString));
            return;
        }

        services.AddDbContextFactory<PostgresBF2Context>(o => o.UseNpgsql(ConnectionString));
        services.AddSingleton<IDbContextFactory<BF2Context>>(sp =>
        {
            var postgres = sp.GetRequiredService<IDbContextFactory<PostgresBF2Context>>();
            return new DelegateContextFactory(postgres.CreateDbContext);
        });
    }

    /// <summary>
    /// Brings the database's schema up to date at startup: the EF migrations on SQLite and Postgres, the idempotent
    /// <see cref="SqlServerSchemaUpdates"/> on SQL Server.
    /// </summary>
    public async Task MigrateAsync(IServiceProvider services, ILogger logger)
    {
        switch (Provider)
        {
            case DatabaseProvider.Sqlite:
            case DatabaseProvider.Postgres:
                logger.LogInformation("Running {Provider} migrations", Provider);
                await using (var context = await services.GetRequiredService<IDbContextFactory<BF2Context>>().CreateDbContextAsync())
                {
                    await context.Database.MigrateAsync();
                }
                break;

            case DatabaseProvider.SqlServer:
                // The idempotent schema changes since Schema.sql's tables (the export cursor on Match)
                logger.LogInformation("Applying SQL Server schema updates");
                await SqlServerSchemaUpdates.ApplyAsync(ConnectionString);
                break;
        }
    }
}
