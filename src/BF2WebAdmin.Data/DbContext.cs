using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BF2WebAdmin.Data;

public class BF2Context : DbContext
{
    //private readonly string _connectionString;

    public DbSet<MapMod> MapMods { get; set; }
    public DbSet<MapModObject> MapModObjects { get; set; }

    public DbSet<Match> Matches { get; set; }
    public DbSet<MatchRound> MatchRounds { get; set; }
    public DbSet<MatchRoundPlayer> MatchRoundPlayers { get; set; }

    public DbSet<Server> Servers { get; set; }
    public DbSet<ServerModule> ServerModules { get; set; }
    public DbSet<ServerPlayerAuth> ServerPlayerAuths { get; set; }

    public BF2Context(DbContextOptions<BF2Context> options) : base(options)
    {
    }

    //public BF2Context(string connectionString)
    //{
    //    _connectionString = connectionString;
    //}

    //protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite(_connectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MatchRoundPlayer>().HasKey(c => new { c.RoundId, c.PlayerHash });
        modelBuilder.Entity<ServerModule>().HasKey(c => new { c.ServerGroup, c.Module });
        modelBuilder.Entity<ServerPlayerAuth>().HasKey(c => new { c.ServerGroup, c.PlayerHash });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Position>().HaveConversion<PositionConverter>();
        configurationBuilder.Properties<Rotation>().HaveConversion<RotationConverter>();
    }
}

/// <summary>
/// Used when creating DB migrations
/// dotnet ef migrations add InitialCreate
/// </summary>
public class BF2ContextFactory : IDesignTimeDbContextFactory<BF2Context>
{
    public BF2Context CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BF2Context>();
        optionsBuilder.UseSqlite("Data Source=BF2WebAdmin.sqlite;Cache=Shared");

        return new BF2Context(optionsBuilder.Options);
    }
}

public class PositionConverter : ValueConverter<Position, string>
{
    public PositionConverter() : base(v => v.ToString(), v => Position.Parse(v)) { }
}

public class RotationConverter : ValueConverter<Rotation, string>
{
    public RotationConverter() : base(v => v.ToString(), v => Rotation.Parse(v)) { }
}
