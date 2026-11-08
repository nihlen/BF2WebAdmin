using System;

namespace BF2WebAdmin.Data.Entities;

/// <summary>
/// Postgres only (table match_round_player_path): a player's paths in a round, apart from the player row so that list
/// queries never read them. The bytes are the gzip of the JSON, the same bytes that
/// <see cref="MatchRoundPlayer.MovementPathJson"/> and <see cref="MatchRoundPlayer.ProjectilePathsJson"/> hold as base64
/// text (and SQL Server and SQLite store). The match repository converts between the two; the rest of WebAdmin only
/// sees the base64 strings. A row exists when the player has at least one path.
/// </summary>
public class MatchRoundPlayerPath
{
    public Guid RoundId { get; set; }
    public string PlayerHash { get; set; }
    public byte[] MovementPath { get; set; }
    public byte[] ProjectilePaths { get; set; }

    /// <summary>The row for <paramref name="player"/>'s paths, or null when it has none.</summary>
    public static MatchRoundPlayerPath From(MatchRoundPlayer player)
    {
        if (player.MovementPathJson == null && player.ProjectilePathsJson == null)
            return null;

        return new MatchRoundPlayerPath
        {
            RoundId = player.RoundId,
            PlayerHash = player.PlayerHash,
            MovementPath = Decode(player.MovementPathJson),
            ProjectilePaths = Decode(player.ProjectilePathsJson),
        };
    }

    /// <summary>Sets <paramref name="player"/>'s base64 path strings from this row.</summary>
    public void CopyTo(MatchRoundPlayer player)
    {
        player.MovementPathJson = Encode(MovementPath);
        player.ProjectilePathsJson = Encode(ProjectilePaths);
    }

    public static byte[] Decode(string base64) => base64 == null ? null : Convert.FromBase64String(base64);

    public static string Encode(byte[] bytes) => bytes == null ? null : Convert.ToBase64String(bytes);
}
