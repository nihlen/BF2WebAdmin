using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Entities;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Builder;
using BF2WebAdmin.Server.Commands.BF2;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.Server.Modules;

/// <summary>
/// The in-game map builder commands (.o, .undo, .clear, .save, .load). They act on the server's <see cref="MapBuilder"/>,
/// the same objects the web map builder shows.
/// </summary>
public class MapModule : IModule,
    IHandleCommandAsync<MapLoadCommand>,
    IHandleCommandAsync<MapSaveCommand>,
    IHandleCommandAsync<MapClearCommand>,
    IHandleCommandAsync<MapObjectCommand>,
    IHandleCommandAsync<MapUndoCommand>
{
    private readonly IGameServer _gameServer;

    public MapModule(IGameServer server)
    {
        _gameServer = server;
    }

    private MapBuilder? Builder => (_gameServer as GameServer)?.Builder;

    private void Error(string message) => _gameServer.GameWriter.SendText($"Error: {message}");

    public async ValueTask HandleAsync(MapLoadCommand command)
    {
        if (Builder is not { } builder)
            return;

        var layout = await builder.FindLayoutAsync(command.Name);
        if (layout == null)
        {
            Error($"Map '{command.Name}' not found");
            return;
        }

        // Crashes or fails on Dalian Plant
        if (_gameServer.Map?.Name == "dalian_plant")
            layout.MapModObjects.RemoveAll(o => o.TemplateName == "oilcistern_02");

        try
        {
            var result = await builder.LoadLayoutAsync(layout, Who(command));
            _gameServer.GameWriter.SendText($"§C1001Loaded map '{layout.Name}' ({result.Added.Count} objects)");
        }
        catch (BuilderException e)
        {
            Error(e.Message);
        }
    }

    public async ValueTask HandleAsync(MapSaveCommand command)
    {
        if (Builder is not { } builder)
            return;

        try
        {
            await builder.SaveLayoutAsync(command.Name, overwrite: false, Who(command));
            _gameServer.GameWriter.SendText($"§C1001Saved map '{command.Name}' ({builder.Items.Count} objects)");
        }
        catch (BuilderException e)
        {
            Error(e.Message);
        }
    }

    public async ValueTask HandleAsync(MapObjectCommand command)
    {
        if (Builder is not { } builder)
            return;

        if (string.IsNullOrWhiteSpace(command.Template))
        {
            Error($"'{command.Template}' is not a valid template");
            return;
        }

        var player = command.Message.Player;
        if (player?.Position == null || player.Rotation == null)
        {
            // Web and Discord users have no position in the game
            Error(".o needs an in-game player position");
            return;
        }

        var parts = command.Template == "carrier"
            ? new[]
            {
                ("us_carrier_wasp_mid", player.Position),
                ("us_carrier_wasp_interior", new Position(player.Position.X, player.Position.Height + 5.173, player.Position.Y - 93.792)),
                ("us_carrier_wasp_front", new Position(player.Position.X, player.Position.Height, player.Position.Y + 88.084)),
                ("us_carrier_wasp_back", new Position(player.Position.X, player.Position.Height, player.Position.Y - 88.095)),
            }
            : new[] { (command.Template, player.Position) };

        foreach (var (template, position) in parts)
        {
            var (_, error) = await builder.AddAsync(MapModObjectKind.Object, template, 0, position, player.Rotation);
            if (error != null)
            {
                Error(error);
                return;
            }

            _gameServer.GameWriter.SendText($"Spawned '{template}' at {position}");
        }
    }

    public async ValueTask HandleAsync(MapClearCommand command)
    {
        if (Builder is not { } builder)
            return;

        try
        {
            await builder.ClearAsync(Who(command));
            _gameServer.GameWriter.SendText("Cleared map");
        }
        catch (BuilderException e)
        {
            Error(e.Message);
        }
    }

    public async ValueTask HandleAsync(MapUndoCommand command)
    {
        if (Builder is not { } builder)
            return;

        try
        {
            var removed = await builder.UndoAsync(command.Count, Who(command));
            _gameServer.GameWriter.SendText($"Removed {removed} recent objects");
        }
        catch (BuilderException e)
        {
            Error(e.Message);
        }
    }

    private static string Who(BaseCommand command) => command.Message?.Player?.Hash ?? command.Message?.Player?.Name ?? "game";
}
