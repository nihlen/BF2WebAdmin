using System;
using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BF2WebAdmin.Data;

/// <summary>
/// The WebAdmin database on Postgres (database bf2webadmin, docs/database.md): the same entities as
/// <see cref="BF2Context"/> (SQLite), with snake_case singular table names, timestamptz in UTC, the paths in their own
/// table (<see cref="MatchRoundPlayerPath"/>), no match_id on match_round_player, no is_likely_spoof on player_join and
/// the export cursor from sequence match_change_seq. Its own migrations (Migrations/Postgres):
/// dotnet ef migrations add Name --context PostgresBF2Context --output-dir Migrations/Postgres
/// </summary>
public class PostgresBF2Context : BF2Context
{
    /// <summary>The sequence of <see cref="Match.ChangeSeq"/>, taken under <see cref="MatchChangeLock"/>.</summary>
    public const string ChangeSeqSequence = "match_change_seq";

    public DbSet<MatchRoundPlayerPath> MatchRoundPlayerPaths { get; set; }

    public PostgresBF2Context(DbContextOptions<PostgresBF2Context> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>(ChangeSeqSequence);

        modelBuilder.Entity<Server>().ToTable("server");
        modelBuilder.Entity<ServerModule>().ToTable("server_module");
        modelBuilder.Entity<ServerPlayerAuth>().ToTable("server_player_auth");
        modelBuilder.Entity<MapMod>().ToTable("map_mod");
        modelBuilder.Entity<MapModObject>().ToTable("map_mod_object");

        modelBuilder.Entity<Match>(e =>
        {
            e.ToTable("match");
            e.Property(m => m.ChangeSeq).IsRequired();
            e.HasIndex(m => m.MatchStart).IsDescending();
        });

        modelBuilder.Entity<MatchRound>(e =>
        {
            e.ToTable("match_round");
            e.Property(r => r.MatchId).IsRequired();
            e.Property(r => r.RoundStart).IsRequired();
            e.Property(r => r.RoundEnd).IsRequired();
            e.Property(r => r.PositionTrackerInterval).HasConversion<decimal>().HasColumnType("numeric(10,5)");
            e.HasOne(r => r.Match).WithMany(m => m.MatchRounds).HasForeignKey(r => r.MatchId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.MatchId, r.RoundStart });
        });

        modelBuilder.Entity<MatchRoundPlayer>(e =>
        {
            e.ToTable("match_round_player");
            // A player reaches its match through the round; the paths live in match_round_player_path
            e.Ignore(p => p.Match);
            e.Ignore(p => p.MatchId);
            e.Ignore(p => p.MovementPathJson);
            e.Ignore(p => p.ProjectilePathsJson);
            e.HasIndex(p => p.PlayerHash);
        });

        modelBuilder.Entity<MatchRoundPlayerPath>(e =>
        {
            e.ToTable("match_round_player_path");
            e.HasKey(p => new { p.RoundId, p.PlayerHash });
            e.HasOne<MatchRoundPlayer>().WithOne().HasForeignKey<MatchRoundPlayerPath>(p => new { p.RoundId, p.PlayerHash }).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerJoin>(e =>
        {
            e.ToTable("player_join");
            e.Ignore(j => j.IsLikelySpoof);
            e.Property(j => j.Id).UseIdentityByDefaultColumn();
            foreach (var property in new[] { nameof(PlayerJoin.Name), nameof(PlayerJoin.Guid), nameof(PlayerJoin.IpAddress), nameof(PlayerJoin.ServerIpAddress) })
            {
                e.Property(property).IsRequired();
                e.Property(property).Metadata.SetMaxLength(null);
            }
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // timestamptz takes only DateTimeKind.Utc; WebAdmin's times are UTC, mostly without a kind
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }
}

/// <summary>
/// For timestamptz: a time without a kind is UTC (as WebAdmin stores it everywhere), a local time is converted, and the
/// value read back is marked UTC.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(v => ToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }

    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

/// <summary>
/// For the Postgres migrations (no database needed to add one):
/// dotnet ef migrations add Name --context PostgresBF2Context --output-dir Migrations/Postgres
/// </summary>
public class PostgresBF2ContextFactory : IDesignTimeDbContextFactory<PostgresBF2Context>
{
    public PostgresBF2Context CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgresBF2Context>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=bf2webadmin");
        return new PostgresBF2Context(optionsBuilder.Options);
    }
}
