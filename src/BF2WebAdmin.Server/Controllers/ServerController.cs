using AspNetCore.Authentication.ApiKey;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Communication.DTOs;
using BF2WebAdmin.Server.Communication.Events;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BF2WebAdmin.Server.Controllers;

[Authorize(AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme},{ApiKeyDefaults.AuthenticationScheme}")]
[Route("/api/v1/servers")]
public class ServerController : Controller
{
    private readonly IGameServerRegistry _registry;

    public ServerController(IGameServerRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public async Task<IActionResult> GetServers()
    {
        var servers = _registry.GetGameServers().Select(s => new Communication.Events.ServerUpdateEvent
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
        var server = _registry.GetGameServer(serverId);
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
                ServerId = server.ServerInfo.ServerId,
                ServerGroup = server.ServerInfo.ServerGroup,
                IpAddress = server.ServerInfo.IpAddress,
                GamePort = server.ServerInfo.GamePort,
                QueryPort = server.ServerInfo.QueryPort,
                DiscordBotToken = server.ServerInfo.DiscordBot.Token,
                DiscordAdminChannel = server.ServerInfo.DiscordBot.AdminChannel,
                DiscordNotificationChannel = server.ServerInfo.DiscordBot.NotificationChannel,
                DiscordMatchResultChannel = server.ServerInfo.DiscordBot.MatchResultChannel,
                AdminKey = server.ServerInfo.AdminKey,
                IsTestServer = server.ServerInfo.IsTestServer
            },
            Maps = server.Maps.Select(m => m.ToDto()),
            Teams = server.Teams.Select(t => t.ToDto()),
            Players = server.Players.Select(p => p.ToDto()).ToList(),
            EventLog = server.Events.Select(e => new EventLogDto { Message = e.Message, Timestamp = e.Time }),
            ChatLog = server.Messages.Select(m => new ChatLogDto { Message = m.Message, Timestamp = m.Time })
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateServer([FromBody] Data.Entities.Server server)
    {
        await _registry.AddOrUpdateServerAsync(server);
        return Ok();
    }

    [HttpPatch("{serverId}")]
    public async Task<IActionResult> UpdateServer(string serverId, [FromBody] Data.Entities.Server server)
    {
        if (serverId != server.ServerId)
            return BadRequest();

        await _registry.AddOrUpdateServerAsync(server);
        return Ok();
    }

    [HttpDelete("{serverId}")]
    public async Task<IActionResult> DeleteServer(string serverId)
    {
        await _registry.RemoveServerAsync(serverId);
        return Ok();
    }
}
