using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Constants;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Modules.BF2;

public class MapModule : IModule,
    IHandleEventAsync<MapChangedEvent>,
    IHandleCommandAsync<MapLoadCommand>,
    IHandleCommandAsync<MapSaveCommand>,
    IHandleCommandAsync<MapClearCommand>,
    IHandleCommand<MapObjectCommand>,
    IHandleCommand<MapUndoCommand>
{
    private const int MaxObjectCount = 1000;
    private readonly IGameServer _gameServer;
    private readonly IMapRepository _mapRepository;
    private Guid _mapId;
    private IList<MapModObject> _mapObjects;

    public MapModule(IGameServer server, IMapRepository mapRepository)
    {
        _gameServer = server;
        _mapRepository = mapRepository;
        _mapId = Guid.NewGuid();
        _mapObjects = new List<MapModObject>();
    }

    public async ValueTask HandleAsync(MapLoadCommand command)
    {
        var map = await GetMatchingMapAsync(command.Name);
        if (map == null)
        {
            _gameServer.GameWriter.SendText($"Error: Map '{command.Name}' not found");
            return;
        }

        if ((_mapObjects.Count + map.MapModObjects.Count()) > MaxObjectCount)
        {
            _gameServer.GameWriter.SendText("Error: Max object count reached, loading this could crash the server. Use .clear first");
            return;
        }

        var currentMap = _gameServer.Map.Name;
        foreach (var mapObject in map.MapModObjects)
        {
            // Why is this ignored - invalid for dalian?
            if (mapObject.TemplateName == "oilcistern_02" && currentMap == "dalian_plant")
                continue;

            SpawnObject(mapObject.TemplateName, mapObject.Position, mapObject.Rotation, false);
        }

        _gameServer.GameWriter.SendText($"§C1001Loaded map '{command.Name}' ({map.MapModObjects.Count()} objects)");
    }

    public async ValueTask HandleAsync(MapSaveCommand command)
    {
        var existingMap = await GetMatchingMapAsync(command.Name);
        if (existingMap != null)
        {
            _gameServer.GameWriter.SendText($"Error: Map '{command.Name}' already exists");
            return;
        }

        var now = DateTime.UtcNow;
        var map = new MapMod
        {
            Id = _mapId,
            Name = command.Name,
            CreatedBy = command.Message.Player.Hash,
            CreatedDate = now,
            EditedDate = now,
            MapModObjects = _mapObjects.ToList()
        };

        await _mapRepository.CreateAsync(map);

        _gameServer.GameWriter.SendText($"§C1001Saved map '{command.Name}' ({_mapObjects.Count} objects)");
    }

    private async Task<MapMod> GetMatchingMapAsync(string name)
    {
        var maps = await _mapRepository.GetAsync();
        var map = maps?.FirstOrDefault(m => m.Name == name);
        if (map == null)
            return null;

        // Get full map with object list
        return await _mapRepository.GetAsync(map.Id);
    }

    public void Handle(MapObjectCommand command)
    {
        if (!IsValidTemplate(command.Template))
        {
            _gameServer.GameWriter.SendText($"Error: '{command.Template}' is not a valid template");
            return;
        }

        if (_mapObjects.Count > MaxObjectCount)
        {
            _gameServer.GameWriter.SendText("Error: Max object count reached, spawning this could crash the server. Use .clear first");
            return;
        }

        var player = command.Message.Player;
        if (player?.Position == null || player.Rotation == null)
        {
            // Web and Discord users have no position in the game
            _gameServer.GameWriter.SendText("Error: .o needs an in-game player position");
            return;
        }

        if (command.Template == "carrier")
        {
            var midPos = player.Position;
            SpawnObject("us_carrier_wasp_mid", midPos, player.Rotation, true);

            var interiorPos = new Position(midPos.X, midPos.Height + 5.173, midPos.Y - 93.792);
            SpawnObject("us_carrier_wasp_interior", interiorPos, player.Rotation, true);

            var frontPos = new Position(midPos.X, midPos.Height, midPos.Y + 88.084);
            SpawnObject("us_carrier_wasp_front", frontPos, player.Rotation, true);

            var backPos = new Position(midPos.X, midPos.Height, midPos.Y - 88.095);
            SpawnObject("us_carrier_wasp_back", backPos, player.Rotation, true);
        }
        else
        {
            SpawnObject(command.Template, player.Position, player.Rotation, true);
        }
    }

    private bool IsValidTemplate(string template)
    {
        return !string.IsNullOrWhiteSpace(template);
    }

    private void SpawnObject(string template, Position position, Rotation rotation, bool showMessage)
    {
        var replacements = new Dictionary<string, string>
        {
            {"{TEMPLATE}", template},
            {"{POSITION}", position.ToString()},
            {"{ROTATION}", rotation.ToString()}
        };

        var script = RconScript.AddObject.Select(line => line.ReplacePlaceholders(replacements));
        _gameServer.GameWriter.SendConsole(script.ToArray());
        _mapObjects.Add(new MapModObject
        {
            Id = Guid.NewGuid(),
            MapModId = _mapId,
            TemplateName = template,
            Position = position,
            Rotation = rotation
        });

        if (showMessage)
            _gameServer.GameWriter.SendText($"Spawned '{template}' at {position}");
    }

    public async ValueTask HandleAsync(MapClearCommand command)
    {
        var playerScores = _gameServer.Players.Select(p => (p, new[] { p.Score.Total, p.Score.Team, p.Score.Kills, p.Score.Deaths }));
        _gameServer.GameWriter.SendRestartMap();
        _mapObjects.Clear();

        _gameServer.GameWriter.SendText("Cleared map");

        // Need to wait for clear or scores will be overwritten again
        await Task.Delay(1000);

        foreach (var (player, score) in playerScores)
        {
            _gameServer.GameWriter.SendScore(player, score[0], score[1], score[2], score[3]);
        }
    }

    public void Handle(MapUndoCommand command)
    {
        _gameServer.GameWriter.SendRestartMap();
        var oldObjects = _mapObjects.Take(_mapObjects.Count - command.Count).ToList();
        _mapObjects.Clear();

        foreach (var mapObject in oldObjects)
            SpawnObject(mapObject.TemplateName, mapObject.Position, mapObject.Rotation, false);

        _gameServer.GameWriter.SendText($"Removed {command.Count} recent objects");
        _gameServer.GameWriter.SendTeleport(command.Message.Player, command.Message.Player.Position);
    }

    public ValueTask HandleAsync(MapChangedEvent e)
    {
        _mapId = Guid.NewGuid();
        _mapObjects = new List<MapModObject>();
        return ValueTask.CompletedTask;
    }
}