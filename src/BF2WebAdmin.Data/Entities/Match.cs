using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;

namespace BF2WebAdmin.Data.Entities;

public class Match
{
    public Guid Id { get; set; }
    public string ServerId { get; set; }
    public string ServerName { get; set; }
    public string Map { get; set; }
    public string Type { get; set; }
    public string TeamAHash { get; set; }
    public string TeamAName { get; set; }
    public int TeamAScore { get; set; }
    public string TeamBHash { get; set; }
    public string TeamBName { get; set; }
    public int TeamBScore { get; set; }
    public DateTime? MatchStart { get; set; }
    public DateTime? MatchEnd { get; set; }

    /// <summary>
    /// The export cursor (/api/export/v1/matches): a new, higher number on every write of the match or one of its rounds,
    /// assigned under <see cref="MatchChangeLock"/> so the numbers commit in order. Null only before the first write.
    /// </summary>
    public long? ChangeSeq { get; set; }

    public List<MatchRound> MatchRounds { get; set; } = new();
}

public class MatchRound
{
    public Guid Id { get; set; }
    public int WinningTeamId { get; set; }
    public double PositionTrackerInterval { get; set; }
    public DateTime? RoundStart { get; set; }
    public DateTime? RoundEnd { get; set; }

    public List<MatchRoundPlayer> MatchRoundPlayers { get; set; } = new();
    public Guid? MatchId { get; set; }
    public Match Match { get; set; }
}

public class MatchRoundPlayer
{
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

    public Guid MatchId { get; set; }
    public Match Match { get; set; }
    public MatchRound Round { get; set; }

}