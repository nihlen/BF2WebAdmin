using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Server.Modules.Heli2v2;

public class ProjectilePath
{
    public Projectile Projectile { get; set; }
    public IList<RoundPosition> Path { get; set; } = new List<RoundPosition>();
}

public class TvMissileSetting
{
    public string Name { get; set; }
    public string Template { get; set; }
    public string DefaultValue { get; set; }
}

public class Match
{
    public IList<Round> Rounds { get; set; }
    public string[] TeamHashes { get; set; }
    public bool IsStarted => MatchStart != null;
    public bool IsFinished => MatchEnd != null;
    public bool IsActive => IsStarted && !IsFinished;

    // Stats properties
    public Guid Id { get; set; }
    public string ServerId { get; set; }
    public string ServerName { get; set; }
    public string Map { get; set; }
    public string Type { get; set; }
    public DateTime? MatchStart { get; set; }
    public DateTime? MatchEnd { get; set; }

    public Match()
    {
        Rounds = new List<Round>();
    }
}

/// <summary>A started 2v2 round, for placing a traced missile: <see cref="Number"/> counts the match's rounds from 1 in start order.</summary>
public sealed class LiveRound(Guid matchId, Guid roundId, int number, DateTime start)
{
    public Guid MatchId { get; } = matchId;
    public Guid RoundId { get; } = roundId;
    public int Number { get; } = number;
    public DateTime Start { get; } = start;
    public DateTime? End { get; set; }
}

public class Round
{
    public IList<RoundPlayer> Players { get; set; } = new List<RoundPlayer>();
    public bool IsStarted => RoundStart != null;
    public bool IsFinished => RoundEnd != null;
    public bool IsActive => IsStarted && !IsFinished;
    public DateTime ReadyTime { get; set; }
    public string WinningTeamHash { get; set; }
    public string LosingTeamHash { get; set; }
    public bool IsPendingCompletion { get; set; }

    // Entity properties
    public Guid Id { get; set; }
    public Guid? MatchId { get; set; }
    public int WinningTeamId { get; set; }

    public double PositionTrackerInterval { get; set; }

    public DateTime? RoundStart { get; set; }
    public DateTime? RoundEnd { get; set; }
}

public class RoundPlayer
{
    // TODO: add vehicle/subvehicle and who made the kills here too
    public Player Player { get; set; }
    public string TeamHash { get; set; }
    public bool IsAlive { get; set; }
    public bool IsReady { get; set; }
    public IList<RoundPosition> LastProjectilePath => ProjectilePaths.LastOrDefault() ?? new List<RoundPosition>();
    public IList<RoundPosition> MovementPath { get; set; } = new List<RoundPosition>();
    public IList<IList<RoundPosition>> ProjectilePaths { get; set; } = new List<IList<RoundPosition>>();
    public int LastProjectileId { get; set; }

    // Entity properties
    public Guid RoundId { get; set; }
    public string PlayerHash { get; set; }
    public string PlayerName { get; set; }
    public int TeamId { get; set; }
    public string SubVehicle { get; set; }
    public bool SaidGo { get; set; }
    public Position StartPosition { get; set; }
    public Position DeathPosition { get; set; }
    public DateTime? DeathTime { get; set; }
    public string KillerHash { get; set; }
    public string KillerWeapon { get; set; }
    public Position KillerPosition { get; set; }
    public string MovementPathJson { get; set; }
    public string ProjectilePathsJson { get; set; }

    public RoundPlayer(Player player, string teamHash, Guid roundId)
    {
        RoundId = roundId;
        Player = player;
        PlayerHash = player.Hash;
        PlayerName = player.Name;
        TeamId = player.Team.Id;
        IsAlive = true;
        IsReady = false;
        TeamHash = teamHash;
        SubVehicle = player.SubVehicleTemplate;
    }

    public override string ToString() => Player?.Name ?? PlayerName;
}

public class RoundPosition
{
    public int Id { get; set; }
    public int Timestamp { get; set; }
    public Position Position { get; set; }
    public Rotation Rotation { get; set; }
    public int Ping { get; set; }
    public override string ToString() => $"{Timestamp}|{Position}|{Rotation}|{Ping}";
}

public class TeamStats
{
    public string TeamHash { get; set; }
    public int Wins { get; set; }
}

public class PlayerStats
{
    public string PlayerHash { get; set; }
    public int PilotCount { get; set; }
    public int GunnerCount { get; set; }
    public int TeamUsCount { get; set; }
    public int TeamChCount { get; set; }
    public int Ah1zCount { get; set; }
    public int Z10Count { get; set; }
    public int TvWinCount { get; set; }
    public int HydraWinCount { get; set; }
    public int MgWinCount { get; set; }
    public int EnemyCrashWinCount { get; set; }
    public int RdyStartCount { get; set; }
    public int GoStartCount { get; set; }
    public double TotalDistanceFromPad { get; set; }
    public double TotalAverageAltitude { get; set; }
    public double TotalAverageSpeed { get; set; }
}
/// <summary>A team's id in a match: its two player hashes in ordinal order, joined.</summary>
public static class TeamHash
{
    public static string Of(Player p1, Player p2) => Of(p1.Hash, p2.Hash);

    public static string Of(IEnumerable<string> playerHashes) => Of(playerHashes.First(), playerHashes.Last());

    public static string Of(string hash1, string hash2) =>
        string.Compare(hash1, hash2, StringComparison.Ordinal) <= 0 ? hash1 + hash2 : hash2 + hash1;
}
