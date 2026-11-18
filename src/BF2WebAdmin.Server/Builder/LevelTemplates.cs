using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.Server.Builder;

/// <summary>
/// Which object templates the running level uses: only those may be spawned. The server knows every template, so it
/// spawns any of them, but a template the level does not use can crash the game (the clients load what the level
/// needs). A template counts when the level placed an object of it: an object in the grid that the builder did not
/// spawn. The answer is kept until the map changes.
/// </summary>
public sealed class LevelTemplates(GameServer server)
{
    /// <summary>Requests in flight at once: the module runs at most 64 commands a tick.</summary>
    private const int Parallel = 16;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, bool> _onLevel = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<uint> _spawned = [];
    private string? _map;

    /// <summary>An object the builder spawned on this level, which does not make its template one the level uses.</summary>
    public void AddSpawned(uint consoleId)
    {
        lock (_lock)
        {
            Reset();
            _spawned.Add(consoleId);
        }
    }

    /// <param name="remember">False for a vehicle: none may be on the map just now (all destroyed and waiting to respawn),
    /// so a no is asked again next time.</param>
    public async Task<bool> IsOnLevelAsync(string template, bool remember = true)
    {
        lock (_lock)
        {
            Reset();
            if (_onLevel.TryGetValue(template, out var known))
                return known;
        }

        bool onLevel;
        try
        {
            var objects = await server.GameWriter.ListObjectsAsync(template, 256);
            lock (_lock)
            {
                // The object cache keeps a deleted object at 0/0/0 under the terrain with a new console id
                onLevel = objects.Any(o => !_spawned.Contains(o.ConsoleId) && (Math.Abs(o.Position.X) > 1 || Math.Abs(o.Position.Z) > 1));
            }
        }
        catch (AdminCommandException e) when (e.Code == AdminErrorCode.Unavailable)
        {
            // No such template
            onLevel = false;
        }

        if (onLevel || remember)
        {
            lock (_lock)
            {
                Reset();
                _onLevel[template] = onLevel;
            }
        }

        return onLevel;
    }

    /// <summary>Those of the names the level uses, in their order.</summary>
    public async Task<IReadOnlyList<string>> FilterAsync(IReadOnlyList<string> templates, CancellationToken cancellationToken)
    {
        var onLevel = new bool[templates.Count];
        await System.Threading.Tasks.Parallel.ForAsync(0, templates.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Parallel, CancellationToken = cancellationToken },
            async (i, _) => onLevel[i] = await IsOnLevelAsync(templates[i]));
        return templates.Where((_, i) => onLevel[i]).ToList();
    }

    /// <summary>A new map forgets the old one's answers and objects. Called under the lock.</summary>
    private void Reset()
    {
        var map = server.Map?.Name?.ToLowerInvariant();
        if (map == _map)
            return;
        _map = map;
        _onLevel.Clear();
        _spawned.Clear();
    }
}
