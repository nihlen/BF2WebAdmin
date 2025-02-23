using AspNetCore.Authentication.ApiKey;
using BF2WebAdmin.Common.Communication;
using BF2WebAdmin.Shared.Communication.DTOs;
using BF2WebAdmin.Shared.Communication.Events;
using Mapster;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BF2WebAdmin.Server.Controllers;

[Authorize(AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme},{ApiKeyDefaults.AuthenticationScheme}")]
[Route("/api/v1/servers")]
public class ServerController : Controller
{
    private readonly ISocketServer _socketServer;

    public ServerController(ISocketServer socketServer)
    {
        _socketServer = socketServer;
    }

    [HttpGet]
    public async Task<IActionResult> GetServers()
    {
        var servers = _socketServer.GetGameServers().Select(s => new Shared.Communication.Events.ServerUpdateEvent
        {
            Id = s.Id,
            Name = s.Name,
            IpAddress = s.IpAddress.ToString(),
            GamePort = s.GamePort,
            QueryPort = s.QueryPort,
            Map = s.Map?.Name,
            Players = s.Players.Count(),
            MaxPlayers = s.MaxPlayers,
            GameState = s.State,
            SocketState = s.SocketState,
            ServerGroup = s.ModManager.ServerSettings.ServerGroup
        });
        return Ok(servers);
    }

    [HttpGet("{serverId}")]
    public async Task<IActionResult> GetServer(string serverId)
    {
        var server = _socketServer.GetGameServer(serverId);
        var result = new ServerSnapshotEvent
        {
            Server = new ServerDto
            {
                Id = server.Id,
                Name = server.Name,
                IpAddress = server.IpAddress.ToString(),
                GamePort = server.GamePort,
                QueryPort = server.QueryPort,
                Map = server.Map?.Name,
                Players = server.Players.Count(),
                MaxPlayers = server.MaxPlayers,
                GameState = server.State,
                SocketState = server.SocketState,
                ServerGroup = server.ModManager.ServerSettings.ServerGroup
            },
            Data = new ServerDataDto
            {
                ServerId = $"{server.ServerInfo.IpAddress}:{server.ServerInfo.GamePort}",
                ServerGroup = server.ServerInfo.ServerGroup,
                IpAddress = server.ServerInfo.IpAddress,
                GamePort = server.ServerInfo.GamePort,
                QueryPort = server.ServerInfo.QueryPort,
                RconPort = server.ServerInfo.RconPort,
                RconPassword = server.ServerInfo.RconPassword,
                DiscordBotToken = server.ServerInfo.DiscordBot.Token,
                DiscordAdminChannel = server.ServerInfo.DiscordBot.AdminChannel,
                DiscordNotificationChannel = server.ServerInfo.DiscordBot.NotificationChannel,
                DiscordMatchResultChannel = server.ServerInfo.DiscordBot.MatchResultChannel
            },
            Maps = server.Maps.Select(m => m.ToDto()),
            Teams = server.Teams.Select(t => t.ToDto()),
            Players = server.Players.Select(p => p.ToDto()).ToList(),
            EventLog = server.Events.Select(e => new EventLogDto { Message = e.Message, Timestamp = e.Time }),
            ChatLog = server.Messages.Select(m => new ChatLogDto { Message = m.Message, Timestamp = m.Time })
        };

        return Ok(result);
    }

    [HttpPost("{serverId}")]
    public async Task<IActionResult> CreateServer(string serverId, [FromBody] Data.Entities.Server server)
    {
        if (serverId != server.ServerId)
            return BadRequest();

        var serverEntity = server.Adapt<Data.Entities.Server>();
        await _socketServer.AddOrUpdateServerAsync(serverEntity);
        return Ok();
    }

    [HttpPatch("{serverId}")]
    public async Task<IActionResult> UpdateServer(string serverId, [FromBody] Data.Entities.Server server)
    {
        if (serverId != server.ServerId)
            return BadRequest();

        var serverEntity = server.Adapt<Data.Entities.Server>();
        await _socketServer.AddOrUpdateServerAsync(serverEntity);
        return Ok();
    }

    [HttpDelete("{serverId}")]
    public async Task<IActionResult> DeleteServer(string serverId)
    {
        await _socketServer.RemoveServerAsync(serverId);
        return Ok();
    }
}
