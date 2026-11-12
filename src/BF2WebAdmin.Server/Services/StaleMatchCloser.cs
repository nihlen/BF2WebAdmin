using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;

namespace BF2WebAdmin.Server.Services;

/// <summary>
/// Ends matches that were left open: Heli2v2Module ends a match when the next one starts or the module stops, so a
/// restart or crash in the middle of a match leaves it without an end. A match whose last round ended (or, without
/// rounds, that started) over an hour ago gets that time as its end and the scores counted from its rounds' winners.
/// Runs at startup and every 10 minutes.
/// </summary>
public sealed class StaleMatchCloser : BackgroundService
{
    public static readonly TimeSpan DeadMatchAge = TimeSpan.FromHours(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StaleMatchCloser> _logger;

    public StaleMatchCloser(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<StaleMatchCloser> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _timeProvider);
        do
        {
            try
            {
                await CloseStaleMatchesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Closing stale matches failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> CloseStaleMatchesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMatchRepository>();

        var closed = 0;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var match in await repository.GetUnfinishedMatchesAsync())
        {
            if (!TryClose(match, now))
                continue;

            await repository.UpdateMatchAsync(match);
            closed++;
            _logger.LogInformation("Closed stale match {MatchId} on {ServerId}: ended {MatchEnd:u}, {TeamAScore}-{TeamBScore}",
                match.Id, match.ServerId, match.MatchEnd, match.TeamAScore, match.TeamBScore);
        }

        return closed;
    }

    /// <summary>Sets the end and the scores of a match that has been idle for <see cref="DeadMatchAge"/>; false if it may still be played.</summary>
    public static bool TryClose(Match match, DateTime utcNow)
    {
        if (match.MatchEnd is not null)
            return false;

        var lastActivity = match.MatchRounds.Max(r => r.RoundEnd) ?? match.MatchStart;
        if (lastActivity is null || utcNow - lastActivity.Value < DeadMatchAge)
            return false;

        match.MatchEnd = lastActivity;
        match.TeamAScore = match.MatchRounds.Count(r => WinningTeamHash(r) == match.TeamAHash);
        match.TeamBScore = match.MatchRounds.Count(r => WinningTeamHash(r) == match.TeamBHash);
        return true;
    }

    /// <summary>As Heli2v2Module's team hash: the two player hashes in ordinal order, joined.</summary>
    private static string? WinningTeamHash(MatchRound round)
    {
        var hashes = round.MatchRoundPlayers.Where(p => p.TeamId == round.WinningTeamId).Select(p => p.PlayerHash).ToList();
        if (hashes.Count == 0)
            return null;

        var (first, last) = (hashes[0], hashes[^1]);
        return string.Compare(first, last, StringComparison.Ordinal) <= 0 ? first + last : last + first;
    }
}
