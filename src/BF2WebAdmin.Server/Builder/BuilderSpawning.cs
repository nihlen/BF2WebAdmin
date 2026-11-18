using System.Globalization;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.Server.Builder;

/// <summary>What an engine object is to its built item.</summary>
public enum PartRole
{
    /// <summary>A static object clients see (a built object, or a helipad's concrete pad with its repair and rearm).</summary>
    Static,

    /// <summary>A spawn point, server only.</summary>
    SpawnPoint,

    /// <summary>A helipad's spawn point that puts the player in its helicopter, in a spawn group of its own. Server only.</summary>
    HeliSpawnPoint,

    /// <summary>A helicopter spawner, server only. It survives round restarts (the engine resets it every round).</summary>
    Spawner,
}

/// <param name="Template">The template its objects are listed by, to see whether it still exists.</param>
public readonly record struct SpawnedPart(PartRole Role, uint ConsoleId, uint NetId, string Template);

/// <summary>
/// How each kind of built item is made of engine objects, and removed again (docs/map-builder.md, "Engine objects").
/// - A static object: <c>SpawnObject</c> after giving its template a networkable info only the server has, so clients
///   create it from their own template.
/// - A spawn point: a SpawnPoint template of its own made with console lines, on the id of the team's control point
///   nearest to it, alone in a spawn group of its own. The player picks a group and the server picks one of its points
///   at random, so a point in the control point's own group would rarely be the one; in its own group it always is.
/// - A helipad: the concrete helipad (its tweak adds the repair and rearm, for the team of the nearest control point),
///   a helicopter spawner of its own for the team, and a spawn point like the above that also seats the player in the
///   helicopter (the nearest free seat within 15 m; on foot on the pad when the helicopter is gone).
/// A new group has no team until its control point's team is set, which the engine does at every round start. A module
/// with SetControlPointTeam (capability bit 7) sets the control point to the team it has right after the point is made,
/// which gives the group its team at once; without, it shows on the spawn screen from the next round restart on. A
/// group the builder made before on the same control point has its team already, so an item that gets a removed one's
/// group works at once either way.
/// Spawn points and spawners are made with the console's <c>Object.create</c>, never <c>SpawnObject</c>: the object cache
/// would pool a deleted one instead of destroying it, and it would stay registered at the origin.
/// </summary>
public sealed class BuilderSpawning(GameServer server)
{
    public const string HelipadTemplate = "concreteblock_helipad";

    /// <summary>The helicopter a team's helipad spawns by default on the 2v2 maps (team 1 China, team 2 US).</summary>
    public static string DefaultHelicopter(int team) => team == 1 ? "ahe_z10" : "ahe_ah1z";

    // Template names made here are unique to this process: a template made by an earlier WebAdmin run keeps its settings
    private readonly string _prefix = $"bf2wa{Random.Shared.Next(0x1000, 0xFFFF):x4}";

    // Control point template -> its controlPointId, per map
    private readonly Dictionary<string, int> _controlPointIds = new(StringComparer.OrdinalIgnoreCase);
    private string? _controlPointIdsMap;

    /// <summary>The templates the level uses, the only ones spawned.</summary>
    public LevelTemplates Level { get; } = new(server);

    private string SpawnPointTemplate(BuiltItem item) => $"{_prefix}_sp_{item.Id}";

    private string SpawnerTemplate(BuiltItem item) => $"{_prefix}_heli_{item.Id}";

    private string HeliSpawnPointTemplate(BuiltItem item) => $"{_prefix}_hsp_{item.Id}";

    /// <summary>
    /// Spawn groups this builder made on the level, with whether a round start has given them their team. The engine
    /// maps each (control point id, group number) pair to a spawn group for the rest of the level: more than 63 static
    /// groups collide with the squads' and it aborts past 127, so the builder makes at most <see cref="MaxSpawnGroups"/>
    /// and gives a removed item's group number to the next item on the same control point.
    /// </summary>
    private readonly Dictionary<(int ControlPointId, int Group), bool> _groups = new();

    // Group number -> the item using it
    private readonly Dictionary<int, int> _groupOwners = new();
    private string? _groupsMap;

    /// <summary>The spawn groups the builder makes per level, beside the level's own (a few) and the squads'.</summary>
    public const int MaxSpawnGroups = 48;

    /// <summary>The first group number the builder uses; the level's own points use 0.</summary>
    public const int FirstGroup = 1001;

    /// <summary>The parts an item consists of, in the order they are made.</summary>
    public IReadOnlyList<(PartRole Role, string Template)> Parts(BuiltItem item) => item.Kind switch
    {
        MapModObjectKind.Object => [(PartRole.Static, item.Template)],
        MapModObjectKind.SpawnPoint => [(PartRole.SpawnPoint, SpawnPointTemplate(item))],
        MapModObjectKind.Helipad =>
        [
            (PartRole.Static, HelipadTemplate),
            (PartRole.Spawner, SpawnerTemplate(item)),
            (PartRole.HeliSpawnPoint, HeliSpawnPointTemplate(item)),
        ],
        _ => throw new BuilderException($"Unknown kind {item.Kind}"),
    };

    /// <summary>Makes the parts of the item that it does not have (all of them for a new item).</summary>
    public async Task SpawnMissingAsync(BuiltItem item, ISet<(string Template, uint ConsoleId)>? present = null)
    {
        foreach (var (role, template) in Parts(item))
        {
            var existing = item.Parts.FindIndex(p => p.Role == role);
            if (existing >= 0)
            {
                if (present == null || present.Contains((template, item.Parts[existing].ConsoleId)))
                    continue;
                item.Parts.RemoveAt(existing);
            }

            var part = role switch
            {
                PartRole.Static => await SpawnStaticAsync(template, item.Position, item.Rotation),
                PartRole.SpawnPoint => await SpawnSpawnPointAsync(item, template, enterVehicle: false),
                PartRole.HeliSpawnPoint => await SpawnSpawnPointAsync(item, template, enterVehicle: true),
                PartRole.Spawner => await SpawnSpawnerAsync(item, template),
                _ => throw new BuilderException($"Unknown part {role}"),
            };
            item.Parts.Add(part);
        }
    }

    public void Delete(BuiltItem item)
    {
        var writer = server.GameWriter;

        // The spawner first, so it does not spawn a helicopter again
        foreach (var part in item.Parts.OrderBy(p => p.Role == PartRole.Spawner ? 0 : 1))
            writer.SendDeleteObject(part.ConsoleId);

        if (item.Kind == MapModObjectKind.Helipad)
            _ = DeleteHelicopterAsync(item);

        item.Parts.Clear();
        FreeGroup(item);
    }

    /// <summary>
    /// A round started: the engine gave every control point's spawn groups their team, the builder's too, so players
    /// can pick them from now on.
    /// </summary>
    public void OnRoundStart()
    {
        ResetGroups();
        foreach (var key in _groups.Keys.ToList())
            _groups[key] = true;
    }

    /// <summary>
    /// The spawn group number for an item's spawn point on a control point: the one it has, else a free one the
    /// builder made on that control point before (it has its team already), else a new one.
    /// </summary>
    private int AllocateGroup(BuiltItem item, int controlPointId)
    {
        ResetGroups();
        if (item.SpawnGroup is { } own && _groups.ContainsKey((controlPointId, own)))
            return own;
        FreeGroup(item);

        var reused = _groups.Keys
            .Where(k => k.ControlPointId == controlPointId && !_groupOwners.ContainsKey(k.Group))
            .OrderByDescending(k => _groups[k])
            .ThenBy(k => k.Group)
            .Select(k => (int?)k.Group)
            .FirstOrDefault();
        var group = reused ?? Enumerable.Range(FirstGroup, MaxSpawnGroups * 4)
            .First(n => !_groupOwners.ContainsKey(n) && !_groups.ContainsKey((controlPointId, n)));
        if (reused == null)
        {
            if (_groups.Count >= MaxSpawnGroups)
                throw new BuilderException(
                    $"this level has the {MaxSpawnGroups} spawn groups the builder may make (the engine keeps each until the map changes); remove a spawn point or helipad near the same base, or change the map");
            _groups[(controlPointId, group)] = false;
        }

        _groupOwners[group] = item.Id;
        item.SpawnGroup = group;
        return group;
    }

    private void FreeGroup(BuiltItem item)
    {
        if (item.SpawnGroup is { } group && _groupOwners.TryGetValue(group, out var owner) && owner == item.Id)
            _groupOwners.Remove(group);
        item.SpawnGroup = null;
    }

    /// <summary>A new map has none of the old one's groups.</summary>
    private void ResetGroups()
    {
        var map = server.Map?.Name?.ToLowerInvariant();
        if (map == _groupsMap)
            return;
        _groupsMap = map;
        _groups.Clear();
        _groupOwners.Clear();
    }

    /// <summary>Whether removing the item leaves a copy on clients that only a resync clears.</summary>
    public static bool NeedsResync(BuiltItem item) => item.Kind != MapModObjectKind.SpawnPoint;

    // -------------------------------------------------------------------------------------------------------------

    private async Task<SpawnedPart> SpawnStaticAsync(string template, Position position, Rotation rotation)
    {
        if (!await Level.IsOnLevelAsync(template))
            throw new BuilderException(NotOnLevel(template));

        var writer = server.GameWriter;
        // Infos last until the map changes, and a duplicate name is a no-op, so this goes before every spawn
        writer.SendConsole(
            "networkableInfo.createNewInfo StaticObjectInfo",
            "networkableInfo.setBasePriority -1",
            $"objecttemplate.active {template}",
            "objecttemplate.setNetworkableInfo StaticObjectInfo");
        var spawned = await writer.SpawnObjectAsync(template, position, rotation);
        Level.AddSpawned(spawned.ConsoleId);
        if (spawned.NetId == 0)
        {
            // Server only: no client would see it, and players would hit an invisible object
            writer.SendDeleteObject(spawned.ConsoleId);
            throw new BuilderException("the template has no networkable, so clients would not see it");
        }

        return new SpawnedPart(PartRole.Static, spawned.ConsoleId, spawned.NetId, template);
    }

    /// <summary>
    /// A spawn point alone in a spawn group of its own on the team's nearest control point: the spawn screen shows it
    /// as a place of its own, and a player who picks it spawns there. (In the control point's own group, the player
    /// would get one of the group's points at random.)
    /// </summary>
    /// <param name="enterVehicle">A helipad's: the point seats the player in the helicopter.</param>
    private async Task<SpawnedPart> SpawnSpawnPointAsync(BuiltItem item, string template, bool enterVehicle)
    {
        var (controlPoint, controlPointId) = await ControlPointAsync(item.Team, item.Position);
        var group = AllocateGroup(item, controlPointId);
        item.SpawnGroupReady = _groups[(controlPointId, group)];
        var lines = new List<string>
        {
            $"ObjectTemplate.create SpawnPoint {template}",
            $"ObjectTemplate.activeSafe SpawnPoint {template}",
            "ObjectTemplate.isNotSaveable 1",
            "ObjectTemplate.setSpawnPositionOffset 0/1.25/0",
            $"ObjectTemplate.setControlPointId {controlPointId}",
            $"ObjectTemplate.group {group}",
        };
        if (enterVehicle)
        {
            lines.Add("ObjectTemplate.setEnterOnSpawn 1");
            lines.Add("ObjectTemplate.setAIEnterOnSpawn 1");
        }

        lines.AddRange(
        [
            $"Object.create {template}",
            $"Object.absolutePosition {item.Position}",
            $"Object.rotation {Number(item.Rotation.Yaw)}/0/0",
            $"Object.name {template}",
        ]);
        server.GameWriter.SendConsole(lines.ToArray());
        var part = await FindCreatedAsync(enterVehicle ? PartRole.HeliSpawnPoint : PartRole.SpawnPoint, template);

        // The group was made with the point and has no team until a round start, so no player could pick it yet:
        // setting the control point to the team it has gives it one, with nothing else happening in the game
        if (!item.SpawnGroupReady && await server.GameWriter.SetControlPointTeamAsync(controlPoint.ConsoleId, controlPoint.Team))
        {
            _groups[(controlPointId, group)] = true;
            item.SpawnGroupReady = true;
        }

        return part;
    }

    private async Task<SpawnedPart> SpawnSpawnerAsync(BuiltItem item, string template)
    {
        // A team other than 0-2 would index past the engine's arrays
        if (item.Team is not (1 or 2))
            throw new BuilderException("a helipad needs team 1 or 2");
        if (!await Level.IsOnLevelAsync(item.Template, remember: false))
            throw new BuilderException($"no {item.Template} is on this map: a helipad spawns a helicopter the level has");

        server.GameWriter.SendConsole(
            $"ObjectTemplate.create ObjectSpawner {template}",
            $"ObjectTemplate.activeSafe ObjectSpawner {template}",
            "ObjectTemplate.isNotSaveable 1",
            "ObjectTemplate.hasMobilePhysics 0",
            $"ObjectTemplate.setObjectTemplate {item.Team} {item.Template}",
            "ObjectTemplate.minSpawnDelay 1",
            "ObjectTemplate.maxSpawnDelay 2",
            $"ObjectTemplate.team {item.Team}",
            $"Object.create {template}",
            $"Object.absolutePosition {Above(item.Position, 0.5)}",
            $"Object.rotation {Number(item.Rotation.Yaw)}/0/0",
            $"Object.name {template}",
            $"Object.team {item.Team}");
        return await FindCreatedAsync(PartRole.Spawner, template);
    }

    /// <summary>The object just made of a template of its own: the only one of it.</summary>
    private async Task<SpawnedPart> FindCreatedAsync(PartRole role, string template)
    {
        var objects = await server.GameWriter.ListObjectsAsync(template, 16);
        if (objects.Count == 0)
            throw new BuilderException($"the {role} was not created");

        var created = objects.MaxBy(o => o.ConsoleId)!;
        return new SpawnedPart(role, created.ConsoleId, created.NetId, template);
    }

    /// <summary>The helicopter of a removed helipad, if it stands on the pad and nobody sits in it (the module refuses those).</summary>
    private async Task DeleteHelicopterAsync(BuiltItem item)
    {
        try
        {
            var helicopters = await server.GameWriter.ListObjectsAsync(item.Template, 256);
            var occupied = server.Players.Where(p => p.Vehicle != null).Select(p => p.Vehicle.RootVehicleId).ToHashSet();
            var onPad = helicopters
                .Where(h => (h.Flags & 1) == 0 && !occupied.Contains((int)h.NetId))
                .Select(h => (Heli: h, Distance: Distance(h.Position, item.Position)))
                .Where(h => h.Distance < 12)
                .OrderBy(h => h.Distance)
                .FirstOrDefault();
            if (onPad.Heli != null)
                server.GameWriter.SendDeleteObject(onPad.Heli.ConsoleId);
        }
        catch (AdminCommandException)
        {
            // Disconnected; the helicopter decays on its own without its spawner
        }
    }

    /// <summary>The team's control point nearest to the position, and its template's controlPointId.</summary>
    private async Task<(AdminControlPointState Point, int ControlPointId)> ControlPointAsync(int team, Position position)
    {
        if (team is not (1 or 2))
            throw new BuilderException("a spawn point needs team 1 or 2");

        var map = server.Map?.Name?.ToLowerInvariant();
        var (pointsMap, points) = server.ControlPoints;
        if (pointsMap != map)
        {
            // The control points of this map come with a new snapshot
            server.Session?.RequestSnapshot();
            for (var i = 0; i < 20 && server.ControlPoints.Map != map; i++)
                await Task.Delay(100);
            (pointsMap, points) = server.ControlPoints;
            if (pointsMap != map)
                throw new BuilderException("the control points of this map are not known yet; try again");
        }

        var nearest = points.Where(p => p.Team == team)
            .MinBy(p => Distance(p.Position, position))
            ?? throw new BuilderException($"team {team} holds no control point to spawn at");

        if (_controlPointIdsMap != map)
        {
            _controlPointIds.Clear();
            _controlPointIdsMap = map;
        }

        if (!_controlPointIds.TryGetValue(nearest.Template, out var id))
        {
            var value = await server.GameWriter.GetTemplateValueAsync("ControlPoint", nearest.Template, "controlPointId");
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || id <= 0)
                throw new BuilderException($"control point {nearest.Template} has no id ('{value}')");
            _controlPointIds[nearest.Template] = id;
        }

        return (nearest, id);
    }

    public static string NotOnLevel(string template) =>
        $"this map's level has no {template}: only the objects the level uses can be spawned (others can crash the game)";

    private static double Distance(Vec3 a, Position b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Height, dz = a.Z - b.Y;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Position Above(Position p, double metres) => new(p.X, p.Height + metres, p.Y);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
