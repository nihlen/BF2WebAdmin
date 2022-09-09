using System.Net;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Shared;

namespace BF2WebAdmin.Server;

public class EventHandler : IEventHandler
{
    private readonly IGameServer _server;

    public EventHandler(IGameServer server)
    {
        _server = server;
    }

    /*
     * Server events
     */
    public async ValueTask OnServerInfoAsync(string serverName, string maps, int gamePort, int queryPort, int maxPlayers, DateTimeOffset time)
    {
        var mapObjects = maps.Split(",")
            .Select(l => l.Split("|"))
            .Select((a, index) => new Map
            {
                Index = index, 
                Name = a.FirstOrDefault(), 
                Size = int.TryParse(a.LastOrDefault(), out var i) ? i : 0
            }).ToList();
        
        await _server.UpdateServerInfoAsync(serverName, gamePort, queryPort, maxPlayers, time);
        await _server.UpdateMapsAsync(mapObjects, time);
    }

    /*
     * Game status events
     */
    public ValueTask OnGameStatePreGameAsync(DateTimeOffset time)
    {
        return _server.UpdateGameStateAsync(GameState.PreGame, time);
    }

    public async ValueTask OnGameStatePlayingAsync(string team1Name, string team2Name, string mapName, int maxPlayers, DateTimeOffset time)
    {
        await _server.UpdateGameStateAsync(GameState.Playing, time);
        await _server.UpdateMapAsync(mapName, team1Name, team2Name, time);
    }

    public ValueTask OnGameStateEndGameAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName, DateTimeOffset time)
    {
        return _server.UpdateGameStateAsync(GameState.EndGame, time);
    }

    public ValueTask OnGameStatePausedAsync(DateTimeOffset time)
    {
        return _server.UpdateGameStateAsync(GameState.Paused, time);
    }

    public ValueTask OnGameStateRestartAsync(DateTimeOffset time)
    {
        return _server.UpdateGameStateAsync(GameState.Restart, time);
    }

    public ValueTask OnGameStateNotConnectedAsync(DateTimeOffset time)
    {
        return _server.UpdateGameStateAsync(GameState.NotConnected, time);
    }

    /*
     * Game events
     */
    public ValueTask OnControlPointCaptureAsync(int teamId, string cpName, DateTimeOffset time)
    {
        throw new NotImplementedException();
    }

    public ValueTask OnControlPointNeutralisedAsync(string cpName, DateTimeOffset time)
    {
        throw new NotImplementedException();
    }

    /*
     * Timer events
     */
    public ValueTask OnTicketStatusAsync(string team1Name, int team1Tickets, string team2Name, int team2Tickets, string mapName, DateTimeOffset time)
    {
        throw new NotImplementedException();
    }

    public ValueTask OnPlayerPositionUpdateAsync(int playerIndex, Position position, Rotation rotation, int ping, DateTimeOffset time)
    {
        var player = _server.GetPlayer(playerIndex);
        return _server.UpdatePlayerAsync(player, position, rotation, ping, time);
    }

    public ValueTask OnProjectilePositionUpdateAsync(int id, string templateName, Position position, Rotation rotation, DateTimeOffset time)
    {
        var projectile = _server.GetProjectile(id, templateName, position);
        return _server.UpdateProjectileAsync(projectile, position, rotation, time);
    }

    /*
     * Player events
     */
    public ValueTask OnPlayerConnectAsync(int index, string name, int pid, string ipAddress, string hash, int teamId, DateTimeOffset time)
    {
        var player = new Player
        {
            Name = name,
            Id = pid,
            Index = index,
            IpAddress = IPAddress.Parse(ipAddress),
            Hash = hash,
            Team = _server.Teams.First(t => t.Id == teamId)
        };
        return _server.AddPlayerAsync(player, time);
    }

    public ValueTask OnPlayerSpawnAsync(int index, Position pos, Rotation rot, DateTimeOffset time)
    {
        var player = _server.GetPlayer(index);
        return _server.SetPlayerSpawnAsync(player, pos, rot, time);
    }

    public ValueTask OnPlayerChangeTeamAsync(int index, int teamId, DateTimeOffset time)
    {
        var player = _server.GetPlayer(index);
        return _server.UpdatePlayerTeamAsync(player, teamId, time);
    }

    public ValueTask OnPlayerScoreAsync(int index, int score, int teamScore, int kills, int deaths, DateTimeOffset time)
    {
        var player = _server.GetPlayer(index);
        return _server.UpdatePlayerScoreAsync(player, teamScore, kills, deaths, score, time);
    }

    public ValueTask OnPlayerRevivedAsync(int medicIndex, int reviveeIndex, DateTimeOffset time)
    {
        throw new NotImplementedException();
    }

    public ValueTask OnPlayerKilledSelfAsync(int index, Position pos, DateTimeOffset time)
    {
        var player = _server.GetPlayer(index);
        return _server.SetPlayerDeathAsync(player, pos, time);
    }

    public ValueTask OnPlayerTeamkilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos, DateTimeOffset time)
    {
        // TODO: declare teamkill?
        //var attacker = _server.GetPlayer(attackerIndex);
        //var victim = _server.GetPlayer(victimIndex);
        //return _server.SetPlayerKillAsync(attacker, attackerPos, victim, victimPos, null, time);

        // Only use the death event for now to be safe
        var player = _server.GetPlayer(victimIndex);
        return _server.SetPlayerDeathAsync(player, victimPos, time);
    }

    public ValueTask OnPlayerKilledAsync(int attackerIndex, Position attackerPos, int victimIndex, Position victimPos, string weapon, DateTimeOffset time)
    {
        var attacker = _server.GetPlayer(attackerIndex);
        var victim = _server.GetPlayer(victimIndex);
        return _server.SetPlayerKillAsync(attacker, attackerPos, victim, victimPos, weapon, time);
    }

    public ValueTask OnPlayerDeathAsync(int index, Position pos, DateTimeOffset time)
    {
        var player = _server.GetPlayer(index);
        return _server.SetPlayerDeathAsync(player, pos, time);
    }

    public ValueTask OnPlayerDisconnectAsync(int index, DateTimeOffset time)
    {
        var player = _server.GetPlayer(index);
        return _server.RemovePlayerAsync(player, time);
    }

    /*
     * Vehicle events
     */
    public ValueTask OnEnterVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName, DateTimeOffset time)
    {
        if (vehicleName.Contains("soldier"))
            return ValueTask.CompletedTask;

        var player = _server.GetPlayer(index);
        var vehicle = _server.GetVehicle(player, rootVehicleId, rootVehicleName, vehicleName);
        return _server.UpdatePlayerVehicleAsync(player, vehicle, vehicleName, time);
    }

    public ValueTask OnExitVehicleAsync(int index, int rootVehicleId, string rootVehicleName, string vehicleName, DateTimeOffset time)
    {
        var player = _server.GetPlayer(index);
        return _server.UpdatePlayerVehicleAsync(player, null, null, time);
    }

    public ValueTask OnVehicleDestroyedAsync(int vehicleId, string vehicleName, DateTimeOffset time)
    {
        // vehicleId is currently always -1 here
        //throw new NotImplementedException();
        return ValueTask.CompletedTask;
    }

    /*
     * Chat events	
     */
    public ValueTask OnChatServerAsync(string channel, string flags, string text, DateTimeOffset time)
    {
        var message = new Message
        {
            Channel = channel,
            Flags = flags,
            Text = text,
            Type = MessageType.Server,
        };
        return _server.AddMessageAsync(message, time);
    }

    public ValueTask OnChatPlayerAsync(string channel, string flags, int index, string text, DateTimeOffset time)
    {
        var message = new Message
        {
            Player = _server.GetPlayer(index),
            Channel = channel,
            Flags = flags,
            Text = text,
            Type = MessageType.Player,
        };
        return _server.AddMessageAsync(message, time);
    }
}