using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BF2WebAdmin.Server;

/// <summary>
/// The database from the configuration: ConnectionStrings:BF2DB and Database:Provider (sqlite, sqlserver or postgres;
/// without it the connection string decides, see <see cref="DatabaseProviders.Resolve"/>). Used by Program.cs, for the
/// web API and the game modules alike. docs/database.md describes the setup.
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
    /// The repositories, on EF Core for every provider (scoped, each call on its own short context from a factory).
    /// <paramref name="maps"/> adds <see cref="IMapRepository"/>, <paramref name="joins"/> <see cref="IJoinHistoryRepository"/>.
    /// </summary>
    public void AddRepositories(IServiceCollection services, bool maps, bool joins)
    {
        AddContextFactory(services);
        services.AddScoped<IServerSettingsRepository, ServerSettingsRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IServerBanRepository, ServerBanRepository>();
        if (maps)
            services.AddScoped<IMapRepository, MapRepository>();
        if (joins)
            services.AddScoped<IJoinHistoryRepository, JoinHistoryRepository>();
    }

    /// <summary>The repositories take an <see cref="IDbContextFactory{BF2Context}"/>; on SQL Server and Postgres it makes the provider's own context.</summary>
    private void AddContextFactory(IServiceCollection services)
    {
        switch (Provider)
        {
            case DatabaseProvider.Sqlite:
                services.AddDbContextFactory<BF2Context>(o => o.UseSqlite(ConnectionString));
                break;

            case DatabaseProvider.SqlServer:
                services.AddDbContextFactory<SqlServerBF2Context>(o => o.UseSqlServer(ConnectionString));
                services.AddSingleton<IDbContextFactory<BF2Context>>(sp =>
                    new DelegateContextFactory(sp.GetRequiredService<IDbContextFactory<SqlServerBF2Context>>().CreateDbContext));
                break;

            case DatabaseProvider.Postgres:
                services.AddDbContextFactory<PostgresBF2Context>(o => o.UseNpgsql(ConnectionString));
                services.AddSingleton<IDbContextFactory<BF2Context>>(sp =>
                    new DelegateContextFactory(sp.GetRequiredService<IDbContextFactory<PostgresBF2Context>>().CreateDbContext));
                break;
        }
    }

    /// <summary>
    /// Brings the database's schema up to date at startup with the provider's EF migrations. On SQL Server the first one
    /// takes over a database from before EF Core (the old Schema.sql) as it is.
    /// </summary>
    public async Task MigrateAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Running {Provider} migrations", Provider);
        await using var context = await services.GetRequiredService<IDbContextFactory<BF2Context>>().CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }
}
