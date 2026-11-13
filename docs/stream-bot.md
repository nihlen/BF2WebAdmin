# Stream bot

The stream bot (bf2-auto-spectator-controller, on a Windows machine with BF2 and OBS) joins a game server as a
spectator and streams it. WebAdmin asks it to start on `.stream` and when a 2v2 match starts, and to stop on
`.stopstream` and when the match ends. The bot reports back, and WebAdmin shows the stream in the Discord bot's status
and in game.

The bot connects to WebAdmin's SignalR hub `/hubs/stream-bot` and keeps the connection open, reconnecting when it
drops. WebAdmin needs nothing else: no message broker, and no route from WebAdmin to the bot. Until 0.1.0 this went over
RabbitMQ with MassTransit.

## Access

- **Network:** the bot needs only an outgoing HTTPS connection to WebAdmin (WebSockets, or long polling as a fallback).
- **Key:** `Authentication:StreamBotKey` in WebAdmin, sent by the bot in the `X-API-KEY` header. It is a scheme of its
  own (`StreamBot/StreamBotAuthentication.cs`): it opens the hub and nothing else. The admin key
  (`Authentication:ApiKey`) does not open the hub, and the bot key does not open the web API. Without the setting no
  bot can connect.
- **What the bot learns:** a game server's id, address, game and query ports, map, size and human player names, and
  the match id.

## Messages

JSON with camelCase names. `StreamBot/StreamBotContracts.cs` has them; the bot has its own copy, so a change needs both.

| Direction | Method | Body |
|---|---|---|
| WebAdmin → bot | `StartStream` | `serverId`, `gameServerIp`, `gameServerPort`, `gameQueryPort`, `matchId`, `state` |
| WebAdmin → bot | `StopStream` | `serverId`, `gameServerIp`, `gameServerPort`, `matchId` |
| bot → WebAdmin | `StreamStarted` | `serverId`, `matchId`, `botName`, `streamUrl` |
| bot → WebAdmin | `StreamStopped` | `serverId`, `matchId` |
| bot → WebAdmin | `GetServerState` | `serverId`; returns the `state` (`map`, `maxPlayers`, `humanPlayers`), or null when the server is not connected |

A start goes to the bot that connected last, a stop to every connected bot. Nothing is queued: with no bot connected,
`.stream` answers "No stream bot is connected" and a match is not streamed. The bot echoes `serverId`, which WebAdmin
uses to find the server.

The bot decides itself whether it can stream a server (`state` in the start request): the map it spectates
(`dalian_2_v_2`), a free slot (more than 4) and a human player other than itself. While it streams it asks
`GetServerState` every 5 minutes and stops (and reports `StreamStopped`) when that no longer holds, so a missed stop
or a server that went away does not leave it streaming. Before 0.1.0 it queried the game server over GameSpy for this.
