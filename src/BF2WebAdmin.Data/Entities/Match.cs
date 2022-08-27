using System;
using System.Collections.Generic;
using BF2WebAdmin.Common.Entities.Game;
using Dapper.Contrib.Extensions;

namespace BF2WebAdmin.Data.Entities;

public class Match
{
    [ExplicitKey] public Guid Id { get; set; }
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

    [Computed] public List<MatchRound> MatchRounds { get; set; } = new();
}

public class MatchRound
{
    [ExplicitKey] public Guid Id { get; set; }
    public int WinningTeamId { get; set; }
    public double PositionTrackerInterval { get; set; }
    public DateTime? RoundStart { get; set; }
    public DateTime? RoundEnd { get; set; }

    [Computed] public List<MatchRoundPlayer> MatchRoundPlayers { get; set; } = new();
    public Guid? MatchId { get; set; }
    [Computed] public Match Match { get; set; }
}

public class MatchRoundPlayer
{
    [ExplicitKey] public Guid RoundId { get; set; }
    [ExplicitKey] public string PlayerHash { get; set; }
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
    [Computed] public Match Match { get; set; }
    [Computed] public MatchRound Round { get; set; }

}