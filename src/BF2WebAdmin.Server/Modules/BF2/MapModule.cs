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
        _gameServer.GameWriter.SendRcon(script.ToArray());
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
        _gameServer.GameWriter.SendRcon(RconScript.RestartMap);
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
        _gameServer.GameWriter.SendRcon(RconScript.RestartMap);
        var oldObjects = _mapObjects.Take(_mapObjects.Count - command.Count).ToList();
        _mapObjects.Clear();

        foreach (var mapObject in oldObjects)
            SpawnObject(mapObject.TemplateName, mapObject.Position, mapObject.Rotation, false);

        _gameServer.GameWriter.SendText($"Removed {command.Count} recent objects");
        _gameServer.GameWriter.SendTeleport(command.Message.Player, command.Message.Player.Position);
    }

    //private async Task TestMessageAsync(Message message)
    //{
    //    if (message.Text == ".sandrosbunker")
    //    {
    //        var delta = -30;
    //        var rotation = new Rotation(0, -180, 0);

    //        var startX = 330;
    //        var endX = -380;

    //        var startY = 330;
    //        var endY = -380;

    //        // [Id],[MapModId],[TemplateName],[Position],[Rotation]
    //        var mapGuid = Guid.NewGuid();
    //        var lines = new List<string>();

    //        var i = 0;
    //        var borderSize = Math.Abs(delta) + 2;
    //        for (var x = startX; x > endX; x += delta)
    //        {
    //            for (var y = startY; y > endY; y += delta)
    //            {
    //                if (i++ > 900)
    //                {
    //                    _gameServer.GameWriter.SendText("Too many objects!");
    //                    return;
    //                }

    //                SpawnObject("coolingtower_01", new Position(x, 258, y), rotation, false, i);
    //                lines.Add($"{Guid.NewGuid()}\t{mapGuid}\tcoolingtower_01\t{new Position(x, 258, y)}\t{rotation}");

    //                var isEdge = Math.Abs(x - startX) < borderSize ||
    //                     Math.Abs(x - endX) < borderSize ||
    //                     Math.Abs(y - startY) < borderSize ||
    //                     Math.Abs(y - endY) < borderSize;

    //                if (isEdge)
    //                {
    //                    SpawnObject("coolingtower_01", new Position(x, 190, y), Rotation.Neutral, false, i);
    //                    lines.Add($"{Guid.NewGuid()}\t{mapGuid}\tcoolingtower_01\t{new Position(x, 190, y)}\t{Rotation.Neutral}");
    //                    SpawnObject("coolingtower_01", new Position(x, 120, y), rotation, false, i);
    //                    lines.Add($"{Guid.NewGuid()}\t{mapGuid}\tcoolingtower_01\t{new Position(x, 120, y)}\t{rotation}");
    //                }

    //                //await Task.Delay(200);
    //            }
    //        }

    //        _gameServer.GameWriter.SendText($"Created {i} towers");
    //        File.WriteAllLines("sandrosbunker.txt", lines);
    //    }

    //    if (message.Text == ".nudes")
    //    {
    //        var startX = 900;
    //        var startZ = 310;
    //        var distanceX = 12.5;
    //        var distanceZ = 11.5;
    //        var i2 = 0;

    //        var mapGuid = Guid.NewGuid();
    //        var lines = new List<string>();

    //        var rotation = Rotation.Neutral;
    //        using var image = Image.Load(@"C:\Users\Alex\Pictures\bf2text.png");
    //        for (var ix = 0; ix < image.Width; ix++)
    //        {
    //            for (var iy = 0; iy < image.Height; iy++)
    //            {
    //                var pixel = image[ix, iy];
    //                if (pixel.A == 0)
    //                    continue;

    //                Logger.LogInformation($"Pixel found at {ix},{iy}");

    //                var xPos = startX - ix * distanceX;
    //                var zPos = startZ - iy * distanceZ;
    //                await Task.Delay(100);
    //                SpawnObject("concrete_pillar_wall", new Position(xPos, zPos, -320), rotation, false);
    //                lines.Add($"{Guid.NewGuid()}\t{mapGuid}\tconcrete_pillar_wall\t{new Position(xPos, zPos, -320)}\t{rotation}");
    //            }
    //        }

    //        File.WriteAllLines("sendnudes.txt", lines);
    //    }

    //}
        
    public ValueTask HandleAsync(MapChangedEvent e)
    {
        _mapId = Guid.NewGuid();
        _mapObjects = new List<MapModObject>();
        return ValueTask.CompletedTask;
    }
}