using BF2WebAdmin.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BF2WebAdmin.Data;

/// <summary>
/// The WebAdmin database on SQL Server: the same entities as <see cref="BF2Context"/> (SQLite), on the tables that
/// WebAdmin's SQL Server databases have had since 2022 (dbo, singular table names, the column types of the old
/// Schema.sql), with the export cursor from sequence dbo.MatchChangeSeq. Its own migrations (Migrations/SqlServer); the
/// first one creates that schema and is idempotent, so a database from before EF Core is taken over as it is:
/// dotnet ef migrations add Name --context SqlServerBF2Context --output-dir Migrations/SqlServer
/// </summary>
public class SqlServerBF2Context : BF2Context
{
    /// <summary>The sequence of <see cref="Match.ChangeSeq"/>, taken under <see cref="MatchChangeLock"/>.</summary>
    public const string ChangeSeqSequence = "MatchChangeSeq";

    public SqlServerBF2Context(DbContextOptions<SqlServerBF2Context> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.HasSequence<long>(ChangeSeqSequence);

        modelBuilder.Entity<MapMod>(e =>
        {
            e.ToTable("MapMod");
            e.Property(m => m.Name).HasMaxLength(50).IsRequired();
            e.Property(m => m.CreatedBy).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<MapModObject>(e =>
        {
            e.ToTable("MapModObject");
            e.Property(o => o.TemplateName).HasMaxLength(100).IsRequired();
            e.Property(o => o.Position).HasMaxLength(50).IsRequired();
            e.Property(o => o.Rotation).HasMaxLength(50).IsRequired();
            e.HasOne(o => o.MapMod).WithMany(m => m.MapModObjects).HasForeignKey(o => o.MapModId)
                .OnDelete(DeleteBehavior.ClientCascade).HasConstraintName("FK_MapModObject_MapMod");
        });

        modelBuilder.Entity<Match>(e =>
        {
            e.ToTable("Match");
            e.Property(m => m.ServerId).HasMaxLength(20).IsRequired();
            e.Property(m => m.ServerName).HasMaxLength(100).IsRequired();
            e.Property(m => m.Map).HasMaxLength(20).IsRequired();
            e.Property(m => m.Type).HasMaxLength(20).IsRequired();
            e.Property(m => m.TeamAHash).HasMaxLength(100).IsRequired();
            e.Property(m => m.TeamAName).HasMaxLength(100).IsRequired();
            e.Property(m => m.TeamBHash).HasMaxLength(100).IsRequired();
            e.Property(m => m.TeamBName).HasMaxLength(100).IsRequired();
            e.Property(m => m.MatchStart).HasColumnType("datetime2(3)").IsRequired();
            e.Property(m => m.MatchEnd).HasColumnType("datetime2(3)");
            e.HasIndex(m => m.ChangeSeq).IsUnique().HasFilter("[ChangeSeq] IS NOT NULL");
        });

        modelBuilder.Entity<MatchRound>(e =>
        {
            e.ToTable("MatchRound");
            e.Property(r => r.MatchId).IsRequired();
            e.Property(r => r.PositionTrackerInterval).HasConversion<decimal>().HasColumnType("decimal(10,5)");
            e.Property(r => r.RoundStart).HasColumnType("datetime2(3)").IsRequired();
            e.Property(r => r.RoundEnd).HasColumnType("datetime2(3)").IsRequired();
            e.HasOne(r => r.Match).WithMany(m => m.MatchRounds).HasForeignKey(r => r.MatchId).IsRequired()
                .OnDelete(DeleteBehavior.ClientCascade).HasConstraintName("FK_MatchRound_Match");
        });

        modelBuilder.Entity<MatchRoundPlayer>(e =>
        {
            e.ToTable("MatchRoundPlayer");
            e.Property(p => p.PlayerHash).HasMaxLength(50);
            e.Property(p => p.PlayerName).HasMaxLength(50).IsRequired();
            e.Property(p => p.SubVehicle).HasMaxLength(50);
            e.Property(p => p.StartPosition).HasMaxLength(32);
            e.Property(p => p.DeathPosition).HasMaxLength(32);
            e.Property(p => p.DeathTime).HasColumnType("datetime2(3)");
            e.Property(p => p.KillerHash).HasMaxLength(50);
            e.Property(p => p.KillerWeapon).HasMaxLength(50);
            e.Property(p => p.KillerPosition).HasMaxLength(32);
            e.HasOne(p => p.Match).WithMany().HasForeignKey(p => p.MatchId)
                .OnDelete(DeleteBehavior.ClientCascade).HasConstraintName("FK_MatchRoundPlayer_Match");
            e.HasOne(p => p.Round).WithMany(r => r.MatchRoundPlayers).HasForeignKey(p => p.RoundId)
                .OnDelete(DeleteBehavior.ClientCascade).HasConstraintName("FK_MatchRoundPlayer_MatchRound");
        });

        modelBuilder.Entity<Server>(e =>
        {
            e.ToTable("Server");
            e.Property(s => s.ServerId).HasMaxLength(20);
            e.Property(s => s.ServerGroup).HasMaxLength(50).IsRequired();
            e.Property(s => s.IpAddress).HasMaxLength(100);
            e.Property(s => s.RconPassword).HasMaxLength(100);
            e.Property(s => s.DiscordBotToken).HasMaxLength(200);
            e.Property(s => s.DiscordAdminChannel).HasMaxLength(50);
            e.Property(s => s.DiscordNotificationChannel).HasMaxLength(50);
            e.Property(s => s.DiscordMatchResultChannel).HasMaxLength(50);
            e.Property(s => s.AdminKey).HasMaxLength(128);
        });

        modelBuilder.Entity<ServerModule>(e =>
        {
            e.ToTable("ServerModule");
            e.Property(m => m.ServerGroup).HasMaxLength(50);
            e.Property(m => m.Module).HasMaxLength(50);
        });

        modelBuilder.Entity<ServerPlayerAuth>(e =>
        {
            e.ToTable("ServerPlayerAuth");
            e.Property(a => a.ServerGroup).HasMaxLength(50);
            e.Property(a => a.PlayerHash).HasMaxLength(50);
        });

        modelBuilder.Entity<PlayerJoin>(e =>
        {
            // int on SQL Server, as other programs' copies of the table have it
            e.Property(j => j.Id).HasConversion<int>().UseIdentityColumn();
            foreach (var property in new[] { nameof(PlayerJoin.Name), nameof(PlayerJoin.Guid), nameof(PlayerJoin.IpAddress), nameof(PlayerJoin.ServerIpAddress) })
                e.Property(property).IsRequired();
        });
    }
}

/// <summary>
/// For the SQL Server migrations (no database needed to add one):
/// dotnet ef migrations add Name --context SqlServerBF2Context --output-dir Migrations/SqlServer
/// </summary>
public class SqlServerBF2ContextFactory : IDesignTimeDbContextFactory<SqlServerBF2Context>
{
    public SqlServerBF2Context CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerBF2Context>().UseSqlServer("Server=localhost;Database=bf2;Integrated Security=true;TrustServerCertificate=true").Options);
}
