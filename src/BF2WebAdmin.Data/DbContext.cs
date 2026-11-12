using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BF2WebAdmin.Data;

public class BF2Context : DbContext
{
    public DbSet<MapMod> MapMods { get; set; }
    public DbSet<MapModObject> MapModObjects { get; set; }

    public DbSet<Match> Matches { get; set; }
    public DbSet<MatchRound> MatchRounds { get; set; }
    public DbSet<MatchRoundPlayer> MatchRoundPlayers { get; set; }

    public DbSet<Server> Servers { get; set; }
    public DbSet<ServerModule> ServerModules { get; set; }
    public DbSet<ServerPlayerAuth> ServerPlayerAuths { get; set; }

    /// <summary>Player joins (dbo.JoinHistory on SQL Server), read by the export API.</summary>
    public DbSet<PlayerJoin> JoinHistory { get; set; }

    /// <summary>Who banned whom and why; the bans themselves are in each game server's own ban list.</summary>
    public DbSet<ServerBan> ServerBans { get; set; }

    public BF2Context(DbContextOptions<BF2Context> options) : base(options)
    {
    }

    /// <summary>For <see cref="PostgresBF2Context"/>, which has its own options type and migrations.</summary>
    protected BF2Context(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MatchRoundPlayer>().HasKey(c => new { c.RoundId, c.PlayerHash });
        modelBuilder.Entity<Match>().HasIndex(m => m.ChangeSeq).IsUnique();
        modelBuilder.Entity<ServerModule>().HasKey(c => new { c.ServerGroup, c.Module });
        modelBuilder.Entity<ServerPlayerAuth>().HasKey(c => new { c.ServerGroup, c.PlayerHash });
        modelBuilder.Entity<PlayerJoin>(e =>
        {
            e.ToTable("JoinHistory");
            e.Property(j => j.Name).HasMaxLength(PlayerJoin.NameLength);
            e.Property(j => j.Guid).HasMaxLength(PlayerJoin.GuidLength);
            e.Property(j => j.IpAddress).HasMaxLength(PlayerJoin.IpAddressLength);
            e.Property(j => j.ServerIpAddress).HasMaxLength(PlayerJoin.IpAddressLength);
        });
        modelBuilder.Entity<ServerBan>(e =>
        {
            e.ToTable("ServerBan");
            e.HasKey(b => new { b.ServerId, b.Identifier });
            e.Property(b => b.ServerId).HasMaxLength(ServerBan.ServerIdLength);
            e.Property(b => b.Identifier).HasMaxLength(ServerBan.IdentifierLength);
            e.Property(b => b.Name).HasMaxLength(ServerBan.NameLength);
            e.Property(b => b.BannedBy).HasMaxLength(ServerBan.BannedByLength);
            e.Property(b => b.Reason).HasMaxLength(ServerBan.ReasonLength);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Position>().HaveConversion<PositionConverter>();
        configurationBuilder.Properties<Rotation>().HaveConversion<RotationConverter>();
    }
}

/// <summary>
/// Used when creating the SQLite migrations (Migrations/):
/// dotnet ef migrations add Name --context BF2Context --output-dir Migrations
/// The Postgres migrations have their own context (<see cref="PostgresBF2Context"/>, Migrations/Postgres/).
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
