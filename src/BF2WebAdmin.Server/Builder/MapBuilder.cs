using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BF2WebAdmin.Server.Builder;

/// <summary>
/// The map builder of one game server: the objects, spawn points and helipads placed on the running map, from the web
/// builder (docs/map-builder.md) or the in-game <c>.o</c> commands.
/// - Adding spawns on the server at once; every client sees it.
/// - Removing deletes on the server at once (collisions and hits are right from then on) and then resyncs each human
///   client, which drops its own copy (admin channel ResyncClient). A client's resync waits while the player is dead
///   (it would lose its spawn point selection) or has a missile in flight (its gunner would lose the guidance).
/// - Moving is removing and adding.
/// - A round restart destroys every spawned object on the server; the builder spawns them again. A new map clears it.
/// </summary>
public sealed class MapBuilder
{
    /// <summary>The most built items at once; more could crash the server (MapModule's old limit).</summary>
    public const int MaxItems = 1000;

    private static readonly TimeSpan ResyncInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ResyncRetryDelay = TimeSpan.FromSeconds(1);

    private readonly GameServer _server;
    private readonly IServiceProvider _services;
    private readonly IHubContext<ServerHub, IServerHubClient>? _hub;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<BuiltItem> _items = new();

    // Player index -> when to try next. Guarded by _pendingLock: the resync loop runs beside apply calls
    private readonly Dictionary<int, PendingResync> _pending = new();
    private readonly object _pendingLock = new();
    private Task? _resyncLoop;

    private readonly BuilderSpawning _spawning;
    private int _nextId = 1;
    private string? _map;

    public MapBuilder(GameServer server, IServiceProvider services, IHubContext<ServerHub, IServerHubClient>? hub, ILogger logger,
        CancellationToken cancellationToken)
    {
        _server = server;
        _services = services;
        _hub = hub;
        _logger = logger;
        _cancellationToken = cancellationToken;
        _spawning = new BuilderSpawning(server);
    }

    /// <summary>Changes since the server started; a client applies its drafts against the revision it has seen.</summary>
    public int Revision { get; private set; }

    /// <summary>The saved layout the items came from or were last saved as, if any.</summary>
    public (Guid Id, string Name)? Layout { get; private set; }

    public IReadOnlyList<BuiltItem> Items
    {
        get { lock (_items) return _items.ToArray(); }
    }

    public BuilderStateDto GetState()
    {
        var players = _server.Players.ToDictionary(p => p.Index);
        List<PendingResyncDto> pending;
        lock (_pendingLock)
        {
            pending = _pending.Keys
                .Select(i => players.GetValueOrDefault(i))
                .Where(p => p != null)
                .Select(p => new PendingResyncDto(p!.Index, p.DisplayName ?? "", ResyncBlocker(p)))
                .ToList();
        }

        return new BuilderStateDto(
            _map ?? _server.Map?.Name?.ToLowerInvariant(),
            Revision,
            _server.Session is { IsOpen: true },
            _server.GameWriter.CanResyncClients,
            Layout?.Id,
            Layout?.Name,
            Items.Select(i => i.ToDto()).ToList(),
            pending);
    }

    // -------------------------------------------------------------------------------------------------------------
    // Changes

    /// <summary>
    /// Applies a batch of drafts: removes, then moves (remove and add), then adds. Ids that no longer exist are skipped
    /// (another admin removed them). Throws <see cref="BuilderException"/> when the batch cannot be applied at all.
    /// </summary>
    public async Task<BuilderApplyResult> ApplyAsync(BuilderApplyRequest request, string user)
    {
        await _lock.WaitAsync(_cancellationToken);
        try
        {
            if (request.Revision != Revision)
                throw new BuilderException($"The builder changed (revision {Revision}, yours {request.Revision}). Reload and apply again.", conflict: true);

            var removing = request.Remove.Concat(request.Update.Select(u => u.Id)).Distinct().Select(Find).OfType<BuiltItem>();
            if (removing.Any(BuilderSpawning.NeedsResync) && !_server.GameWriter.CanResyncClients)
                throw new BuilderException("This server's module cannot resync clients, so objects and helipads cannot be moved or removed without a round restart.");

            EnsureConnected();
            var count = Items.Count - request.Remove.Count + request.Add.Count;
            if (count > MaxItems)
                throw new BuilderException($"At most {MaxItems} objects can be built; this would make {count}.");

            foreach (var add in request.Add)
                Validate(add.Kind, add.Template, add.Team, add.Position, add.Rotation);
            foreach (var update in request.Update)
                ValidatePlacement(update.Position, update.Rotation);

            var errors = new List<string>();
            var deleted = 0;

            foreach (var id in request.Remove)
            {
                var item = Find(id);
                if (item == null)
                    continue;

                DeleteParts(item);
                lock (_items)
                    _items.Remove(item);
                if (BuilderSpawning.NeedsResync(item))
                    deleted++;
            }

            foreach (var update in request.Update)
            {
                var item = Find(update.Id);
                if (item == null)
                    continue;

                DeleteParts(item);
                if (BuilderSpawning.NeedsResync(item))
                    deleted++;
                item.Position = ToPosition(update.Position);
                item.Rotation = ToRotation(update.Rotation);
                await SpawnPartsAsync(item, errors);
            }

            var added = new List<int>();
            foreach (var add in request.Add)
            {
                var item = new BuiltItem(_nextId++, add.Kind, TemplateFor(add.Kind, add.Template, add.Team), add.Team, ToPosition(add.Position), ToRotation(add.Rotation));
                if (await SpawnPartsAsync(item, errors))
                {
                    lock (_items)
                        _items.Add(item);
                    added.Add(item.Id);
                }
            }

            if (deleted > 0)
                ResyncEveryone();

            Revision++;
            _logger.LogInformation("Builder {ServerId}: {User} applied {Added} added, {Updated} moved, {Removed} removed ({Errors} errors)",
                _server.Id, user, added.Count, request.Update.Count, request.Remove.Count, errors.Count);
            await PublishAsync();
            return new BuilderApplyResult(GetState(), added, errors);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Adds one item, as the in-game <c>.o</c> command does; returns it, or null with the reason in <paramref name="error"/>.</summary>
    public async Task<(BuiltItem? Item, string? Error)> AddAsync(MapModObjectKind kind, string template, int team, Position position, Rotation rotation)
    {
        await _lock.WaitAsync(_cancellationToken);
        try
        {
            EnsureConnected();
            if (Items.Count >= MaxItems)
                return (null, $"At most {MaxItems} objects can be built. Use .clear first");

            var errors = new List<string>();
            var item = new BuiltItem(_nextId++, kind, template, team, position, rotation);
            if (!await SpawnPartsAsync(item, errors))
                return (null, errors.FirstOrDefault() ?? $"'{template}' could not be spawned");

            lock (_items)
                _items.Add(item);
            Revision++;
            await PublishAsync();
            return (item, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes everything built. With a module that can resync clients, nobody notices more than a hitch; without, the
    /// round restarts (as the old <c>.clear</c> did), which destroys every spawned object.
    /// </summary>
    public async Task<bool> ClearAsync(string user, bool keepLayout = false)
    {
        await _lock.WaitAsync(_cancellationToken);
        try
        {
            EnsureConnected();
            var restarted = await ClearLockedAsync();
            if (!keepLayout)
                Layout = null;
            Revision++;
            _logger.LogInformation("Builder {ServerId}: {User} cleared it{How}", _server.Id, user, restarted ? " with a round restart" : "");
            await PublishAsync();
            return restarted;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes the last <paramref name="count"/> items (the in-game <c>.undo</c>). Without a module that can resync
    /// clients the round restarts, and the round start spawns the rest again.
    /// </summary>
    public async Task<int> UndoAsync(int count, string user)
    {
        var last = Items.TakeLast(Math.Max(0, count)).Select(i => i.Id).ToList();
        if (last.Count == 0)
            return 0;

        if (_server.GameWriter.CanResyncClients)
        {
            await ApplyAsync(new BuilderApplyRequest(Revision, [], [], last), user);
            return last.Count;
        }

        await _lock.WaitAsync(_cancellationToken);
        try
        {
            EnsureConnected();
            foreach (var item in Items.Where(i => last.Contains(i.Id)))
                DeleteParts(item);
            lock (_items)
                _items.RemoveAll(i => last.Contains(i.Id));
            RestartRound();
            Revision++;
            await PublishAsync();
            return last.Count;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Restarts the round, which destroys every spawned object, and puts the players' scores back afterwards.</summary>
    private void RestartRound()
    {
        var scores = _server.Players.Select(p => (Player: p, p.Score.Total, p.Score.Team, p.Score.Kills, p.Score.Deaths)).ToList();
        _server.GameWriter.SendRestartMap();
        _ = Task.Run(async () =>
        {
            // The restart sets the scores to 0 first
            await Task.Delay(1000, _cancellationToken);
            foreach (var s in scores)
                _server.GameWriter.SendScore(s.Player, s.Total, s.Team, s.Kills, s.Deaths);
        }, _cancellationToken);
    }

    private async Task<bool> ClearLockedAsync()
    {
        var items = Items;
        lock (_items)
            _items.Clear();

        if (items.Count == 0)
            return false;

        // A spawner outlives the restart, the rest goes with it but is deleted all the same
        foreach (var item in items)
            DeleteParts(item);

        if (!_server.GameWriter.CanResyncClients)
        {
            RestartRound();
            return true;
        }

        ResyncEveryone();
        return false;
    }

    /// <summary>Those of the templates the running level uses (<see cref="LevelTemplates"/>), the only ones that can be built.</summary>
    public async Task<IReadOnlyList<string>> FilterTemplatesAsync(IReadOnlyList<string> templates, CancellationToken cancellationToken)
    {
        if (templates.Count > 4096 || templates.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 100))
            throw new BuilderException("At most 4096 template names of 1 to 100 characters.");

        EnsureConnected();
        try
        {
            return await _spawning.Level.FilterAsync(templates.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), cancellationToken);
        }
        catch (AdminCommandException e)
        {
            throw new BuilderException($"The game server did not answer: {e.Message}");
        }
    }

    // -------------------------------------------------------------------------------------------------------------
    // Layouts

    /// <summary>The saved layouts this map can load: its own, and those from before the builder (no map).</summary>
    public async Task<IReadOnlyList<LayoutDto>> GetLayoutsAsync()
    {
        var map = CurrentMap();
        using var scope = _services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMapRepository>();
        var layouts = await repository.GetAsync();
        return layouts
            .Where(l => l.Map == null || string.Equals(l.Map, map, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.Map == null)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(l => new LayoutDto(l.Id, l.Name, l.Map, l.CreatedBy, l.EditedDate))
            .ToList();
    }

    /// <summary>Saves what is built as a layout of this map. An existing name is replaced only with <paramref name="overwrite"/>.</summary>
    public async Task<LayoutDto> SaveLayoutAsync(string name, bool overwrite, string user)
    {
        name = name.Trim();
        if (name.Length is 0 or > 50)
            throw new BuilderException("A layout name has 1 to 50 characters.");

        var map = CurrentMap() ?? throw new BuilderException("No map is running.");
        await _lock.WaitAsync(_cancellationToken);
        try
        {
            using var scope = _services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IMapRepository>();
            var existing = (await repository.GetAsync())
                .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
                                     && (l.Map == null || string.Equals(l.Map, map, StringComparison.OrdinalIgnoreCase)));
            if (existing != null && !overwrite)
                throw new BuilderException($"A layout '{existing.Name}' exists. Save over it, or pick another name.", conflict: true);

            var now = DateTime.UtcNow;
            var layout = new MapMod
            {
                Id = Guid.NewGuid(),
                Name = name,
                Map = map,
                CreatedBy = user,
                CreatedDate = existing?.CreatedDate ?? now,
                EditedDate = now,
            };
            layout.MapModObjects = Items.Select(i => new MapModObject
            {
                Id = Guid.NewGuid(),
                MapModId = layout.Id,
                Kind = i.Kind,
                Team = i.Team,
                TemplateName = i.Template,
                Position = i.Position,
                Rotation = i.Rotation,
            }).ToList();

            // Replacing is delete and create: the objects are a new set, and EF's update of a graph would keep the old rows
            if (existing != null)
                await repository.DeleteAsync(existing.Id);
            await repository.CreateAsync(layout);

            Layout = (layout.Id, layout.Name);
            Revision++;
            await PublishAsync();
            return new LayoutDto(layout.Id, layout.Name, layout.Map, layout.CreatedBy, layout.EditedDate);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Replaces what is built with a saved layout. Returns the number of items spawned and the errors.</summary>
    public async Task<BuilderApplyResult> LoadLayoutAsync(Guid id, string user)
    {
        MapMod? layout;
        using (var scope = _services.CreateScope())
            layout = await scope.ServiceProvider.GetRequiredService<IMapRepository>().GetAsync(id);
        if (layout == null)
            throw new BuilderException("No such layout.", notFound: true);

        return await LoadLayoutAsync(layout, user);
    }

    public async Task<BuilderApplyResult> LoadLayoutAsync(MapMod layout, string user)
    {
        if (layout.MapModObjects.Count > MaxItems)
            throw new BuilderException($"The layout has {layout.MapModObjects.Count} objects, more than {MaxItems}.");

        await _lock.WaitAsync(_cancellationToken);
        try
        {
            EnsureConnected();
            if (Items.Count > 0 && !_server.GameWriter.CanResyncClients)
                throw new BuilderException("Clear first: this server's module cannot remove what is built without a round restart.");

            await ClearLockedAsync();
            var errors = new List<string>();
            var added = new List<int>();
            foreach (var o in layout.MapModObjects)
            {
                var item = new BuiltItem(_nextId++, o.Kind, TemplateFor(o.Kind, o.TemplateName, o.Team), o.Team, o.Position, o.Rotation);
                if (await SpawnPartsAsync(item, errors))
                {
                    lock (_items)
                        _items.Add(item);
                    added.Add(item.Id);
                }
            }

            Layout = (layout.Id, layout.Name);
            Revision++;
            _logger.LogInformation("Builder {ServerId}: {User} loaded layout {Layout} ({Count} items, {Errors} errors)",
                _server.Id, user, layout.Name, added.Count, errors.Count);
            await PublishAsync();
            return new BuilderApplyResult(GetState(), added, errors);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteLayoutAsync(Guid id)
    {
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMapRepository>().DeleteAsync(id);
        if (Layout?.Id == id)
        {
            Layout = null;
            Revision++;
            await PublishAsync();
        }
    }

    /// <summary>A layout by name for this map (the in-game <c>.load</c>), its own before one from before the builder.</summary>
    public async Task<MapMod?> FindLayoutAsync(string name)
    {
        var map = CurrentMap();
        using var scope = _services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMapRepository>();
        var match = (await repository.GetAsync())
            .Where(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
                        && (l.Map == null || string.Equals(l.Map, map, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(l => l.Map == null)
            .FirstOrDefault();
        return match == null ? null : await repository.GetAsync(match.Id);
    }

    // -------------------------------------------------------------------------------------------------------------
    // Round and connection

    /// <summary>
    /// A round started (a new map, a restart, or the game began once enough players joined), or the module connected.
    /// A new map clears the builder. On the same map, whatever is missing in the game is spawned again: a round reset
    /// destroys every spawned object, a link drop does not, and a restarted server has none. The check waits a second
    /// for the reset's sweep to be done.
    /// </summary>
    public async Task OnRoundStartAsync(string map)
    {
        map = map.ToLowerInvariant();
        try
        {
            if (_map == map && Items.Count > 0)
                await Task.Delay(TimeSpan.FromSeconds(1), _cancellationToken);

            await _lock.WaitAsync(_cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (_map != map)
            {
                var had = Items.Count;
                lock (_items)
                    _items.Clear();
                lock (_pendingLock)
                    _pending.Clear();
                _map = map;
                Layout = null;
                Revision++;
                if (had > 0)
                    _logger.LogInformation("Builder {ServerId}: map is now {Map}, {Count} built items dropped", _server.Id, map, had);
                await PublishAsync();
                return;
            }

            // The round start set the team of every spawn group, the builder's new ones too. After connecting this is a
            // guess: a link drop has no round start, but the groups made before it had one or wait for the next
            _spawning.OnRoundStart();
            var readied = Items.Count(i => !i.SpawnGroupReady && i.Kind != MapModObjectKind.Object);
            foreach (var item in Items)
                item.SpawnGroupReady = true;

            var errors = new List<string>();
            var respawned = 0;
            var present = new HashSet<(string, uint)>();
            foreach (var template in Items.SelectMany(i => i.Parts).Select(p => p.Template).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var o in await _server.GameWriter.ListObjectsAsync(template))
                    present.Add((template, o.ConsoleId));
            }

            foreach (var item in Items)
            {
                var before = item.Parts.ToList();
                await SpawnPartsAsync(item, errors, present);
                if (!before.SequenceEqual(item.Parts))
                    respawned++;
            }

            if (respawned > 0 || readied > 0)
            {
                Revision++;
                _logger.LogInformation("Builder {ServerId}: spawned {Count} missing items again ({Errors} errors)", _server.Id, respawned, errors.Count);
                await PublishAsync();
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Builder {ServerId}: checking the built items for the new round failed", _server.Id);
        }
        finally
        {
            _lock.Release();
        }
    }

    // -------------------------------------------------------------------------------------------------------------
    // Spawning

    /// <summary>Makes the parts the item lacks: all of a new one, or those not in <paramref name="present"/> (template, console id).</summary>
    private async Task<bool> SpawnPartsAsync(BuiltItem item, List<string> errors, ISet<(string, uint)>? present = null)
    {
        try
        {
            await _spawning.SpawnMissingAsync(item, present);
            return true;
        }
        catch (Exception e) when (e is AdminCommandException or BuilderException)
        {
            // Nothing half made stays behind
            if (present == null)
                DeleteParts(item);
            errors.Add($"{item.Template}: {e.Message}");
            _logger.LogWarning("Builder {ServerId}: {Kind} {Template} not spawned: {Error}", _server.Id, item.Kind, item.Template, e.Message);
            return false;
        }
    }

    private void DeleteParts(BuiltItem item)
    {
        _spawning.Delete(item);
    }

    // -------------------------------------------------------------------------------------------------------------
    // Resync

    private sealed class PendingResync
    {
        public DateTime NotBefore;
        public int Failures;
    }

    /// <summary>Every human client drops its stale copies, each as soon as it can (<see cref="ResyncBlocker"/>).</summary>
    private void ResyncEveryone()
    {
        lock (_pendingLock)
        {
            foreach (var player in _server.Players.Where(p => !p.IsBot))
            {
                if (_pending.TryGetValue(player.Index, out var existing))
                    existing.Failures = 0;
                else
                    _pending[player.Index] = new PendingResync { NotBefore = DateTime.MinValue };
            }

            if (_pending.Count > 0 && (_resyncLoop == null || _resyncLoop.IsCompleted))
                _resyncLoop = Task.Run(ResyncLoopAsync, _cancellationToken);
        }
    }

    /// <summary>Why a player's resync waits: "dead", "missile", or null when it can go now.</summary>
    private string? ResyncBlocker(Player player)
    {
        if (!player.IsAlive)
            return "dead";

        // The gunner of a TV missile loses its guidance in a resync
        if (_server.Projectiles.Any(p => p.Owner?.Index == player.Index))
            return "missile";

        return null;
    }

    private async Task ResyncLoopAsync()
    {
        var changed = false;
        while (!_cancellationToken.IsCancellationRequested)
        {
            List<(int Index, PendingResync Pending)> due;
            lock (_pendingLock)
            {
                if (_pending.Count == 0)
                    break;
                due = _pending.Where(p => p.Value.NotBefore <= DateTime.UtcNow).Select(p => (p.Key, p.Value)).ToList();
            }

            foreach (var (index, pending) in due)
            {
                var player = _server.FindPlayer(index);
                if (player == null || player.IsBot)
                {
                    lock (_pendingLock)
                        _pending.Remove(index);
                    changed = true;
                    continue;
                }

                if (ResyncBlocker(player) != null)
                    continue;

                if (await _server.GameWriter.ResyncClientAsync(player))
                {
                    lock (_pendingLock)
                        _pending.Remove(index);
                    changed = true;
                    continue;
                }

                // Not playing yet, or still acknowledging an earlier reset; a client that never can goes after 30 tries
                pending.Failures++;
                pending.NotBefore = DateTime.UtcNow + ResyncRetryDelay;
                if (pending.Failures >= 30)
                {
                    _logger.LogWarning("Builder {ServerId}: gave up resyncing {Player}", _server.Id, player.DisplayName);
                    lock (_pendingLock)
                        _pending.Remove(index);
                    changed = true;
                }
            }

            if (changed)
            {
                changed = false;
                await PublishAsync();
            }

            try
            {
                await Task.Delay(ResyncInterval, _cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        await PublishAsync();
    }

    // -------------------------------------------------------------------------------------------------------------

    private BuiltItem? Find(int id)
    {
        lock (_items)
            return _items.FirstOrDefault(i => i.Id == id);
    }

    private string? CurrentMap() => _map ?? _server.Map?.Name?.ToLowerInvariant();

    private void EnsureConnected()
    {
        if (_server.Session is not { IsOpen: true })
            throw new BuilderException("The game server is not connected.");
    }

    private static void Validate(MapModObjectKind kind, string template, int team, double[] position, double[] rotation)
    {
        switch (kind)
        {
            case MapModObjectKind.Object:
                if (string.IsNullOrWhiteSpace(template) || template.Length > 100 || template.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                    throw new BuilderException($"'{template}' is not a template name.");
                break;
            case MapModObjectKind.SpawnPoint or MapModObjectKind.Helipad:
                if (team is not (1 or 2))
                    throw new BuilderException("A spawn point or helipad needs team 1 or 2.");
                if (kind == MapModObjectKind.Helipad && template?.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) == true)
                    throw new BuilderException($"'{template}' is not a helicopter template.");
                break;
            default:
                throw new BuilderException($"Unknown kind {kind}.");
        }

        ValidatePlacement(position, rotation);
    }

    private static void ValidatePlacement(double[] position, double[] rotation)
    {
        // The admin channel's limits: x and z within ±8192, height -1000 to 10000
        if (position is not { Length: 3 } || rotation is not { Length: 3 } || position.Concat(rotation).Any(v => !double.IsFinite(v))
            || Math.Abs(position[0]) > 8192 || Math.Abs(position[2]) > 8192 || position[1] is < -1000 or > 10000)
            throw new BuilderException("A position is [x, height, z] inside the world and a rotation [yaw, pitch, roll] in degrees.");
    }

    /// <summary>A spawn point has no template of its own; a helipad's is the helicopter it spawns, the team's by default.</summary>
    private static string TemplateFor(MapModObjectKind kind, string? template, int team) => kind switch
    {
        MapModObjectKind.SpawnPoint => "spawnpoint",
        MapModObjectKind.Helipad => string.IsNullOrWhiteSpace(template) || template == "helipad" ? BuilderSpawning.DefaultHelicopter(team) : template.Trim(),
        _ => template?.Trim() ?? "",
    };

    private static Position ToPosition(double[] v) => new(v[0], v[1], v[2]);

    private static Rotation ToRotation(double[] v) => new(v[0], v[1], v[2]);

    private async Task PublishAsync()
    {
        if (_hub == null)
            return;

        try
        {
            await _hub.Clients.Group(_server.Id).BuilderEvent(GetState());
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Builder {ServerId}: state not sent to the web clients", _server.Id);
        }
    }
}

/// <summary>A built object, spawn point or helipad, and the engine objects that make it.</summary>
public sealed class BuiltItem(int id, MapModObjectKind kind, string template, int team, Position position, Rotation rotation)
{
    public int Id { get; } = id;
    public MapModObjectKind Kind { get; } = kind;
    public string Template { get; } = template;
    public int Team { get; } = team;
    public Position Position { get; set; } = position;
    public Rotation Rotation { get; set; } = rotation;

    /// <summary>The engine objects spawned for it this round (console ids), deleted when it is removed.</summary>
    public List<SpawnedPart> Parts { get; } = new();

    /// <summary>The spawn group number of its spawn point (a spawn point's or helipad's), on the nearest control point.</summary>
    public int? SpawnGroup { get; set; }

    /// <summary>
    /// The spawn group of its spawn point has been through a round start, which gives it its team: until then no player
    /// can pick it (<see cref="BuilderSpawning"/>).
    /// </summary>
    public bool SpawnGroupReady { get; set; }

    public BuiltItemDto ToDto() => new(Id, Kind, Template, Team,
        [Position.X, Position.Height, Position.Y], [Rotation.Yaw, Rotation.Pitch, Rotation.Roll], Parts.Count > 0,
        Kind == MapModObjectKind.Object || SpawnGroupReady);
}

public class BuilderException(string message, bool conflict = false, bool notFound = false) : Exception(message)
{
    public bool Conflict { get; } = conflict;
    public bool NotFound { get; } = notFound;
}
