using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Shared;
using Microsoft.Extensions.Caching.Memory;
using Nihlen.Common.Telemetry;
using Polly;

namespace BF2WebAdmin.Server;

public interface ISocketServer
{
    IGameServer? GetGameServer(string serverId);
    IEnumerable<IGameServer> GetGameServers(string? serverGroup = null);
    Task<IEnumerable<ServerInfo>> GetDisconnectedServersAsync();
    Task StartAsync(CancellationToken cancellationToken);
    Task AddOrUpdateServerAsync(Data.Entities.Server server);
    Task RemoveServerAsync(string serverId);
}

public class SocketServer : ISocketServer
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly IEnumerable<ServerInfo>? _serverInfoFromConfig;
    private readonly IServiceProvider _globalServices;
    private readonly bool _logSend;
    private readonly bool _logRecv;
    private readonly ConcurrentDictionary<IPEndPoint, TcpClient> _connections;
    private readonly ConcurrentDictionary<string, GameServerConnectionContext> _servers;
    private readonly ILogger<SocketServer> _logger;
    private readonly IMemoryCache _cache;
    private CancellationToken? _serverCancellationToken;
    private CancellationTokenSource? _serverReconnectCancellationTokenSource;

    public IPAddress GetIpAddress() => _ipAddress;
    public IGameServer? GetGameServer(string serverId) => _servers.TryGetValue(serverId, out var serverContext) ? serverContext.GameServer : null;
    public IEnumerable<IGameServer> GetGameServers(string? serverGroup = null) => _servers.Values.Where(s => s.GameServer.ModManager?.ServerSettings?.ServerGroup == serverGroup || serverGroup is null).Select(c => c.GameServer).ToList();
    public async Task<IEnumerable<ServerInfo>> GetDisconnectedServersAsync() => (await GetServerInfoAsync()).Where(si => !_servers.ContainsKey($"{si.IpAddress}:{si.GamePort}"));

    public SocketServer(IPAddress ipAddress, int port, IEnumerable<ServerInfo>? serverInfoFromConfig, IServiceProvider globalServices, bool logSend, bool logRecv)
    {
        _ipAddress = ipAddress;
        _port = port;
        _serverInfoFromConfig = serverInfoFromConfig;
        _globalServices = globalServices;
        _cache = globalServices.GetRequiredService<IMemoryCache>();
        _logger = globalServices.GetRequiredService<ILogger<SocketServer>>();
        _logSend = logSend;
        _logRecv = logRecv;
        _connections = new ConcurrentDictionary<IPEndPoint, TcpClient>();
        _servers = new ConcurrentDictionary<string, GameServerConnectionContext>();

        // TODO: Move to BF2WebAdminMeter.cs? How to access servers?
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa.server.connected.count", () => _servers.Values.Count(c => c.GameServer.SocketState == SocketState.Connected), description: "Connected game servers");
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa.player.count", () => _servers.Values.Sum(c => c.GameServer.Players.Count()), description: "Players on all servers");
    }

    private async ValueTask<IEnumerable<ServerInfo>> GetServerInfoAsync()
    {
        return await _cache.GetOrCreateAsync<IEnumerable<ServerInfo>>(nameof(GetServerInfoAsync), async entry =>
        {
            using var serviceScope = _globalServices.CreateScope();
            var serverRepository = serviceScope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();

            // Combine server info from config and DB
            entry.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(5);

            var result = new List<ServerInfo>();
            var servers = await serverRepository.GetServersAsync();
            result.AddRange(servers.Select(s => new ServerInfo
            {
                IpAddress = s.IpAddress,
                GamePort = s.GamePort,
                QueryPort = s.QueryPort,
                RconPort = s.RconPort,
                RconPassword = s.RconPassword,
                ServerGroup = s.ServerGroup,
                DiscordBot = new ServerInfo.DiscordBotConfig
                {
                    Token = s.DiscordBotToken,
                    AdminChannel = s.DiscordAdminChannel,
                    NotificationChannel = s.DiscordNotificationChannel,
                    MatchResultChannel = s.DiscordMatchResultChannel
                }
            }));

            // Prioritize servers from the DB and fill in non-duplicates from app settings
            if (_serverInfoFromConfig?.Any() ?? false)
            {
                result.AddRange(_serverInfoFromConfig.Where(s => !result.Any(r => r.IpAddress == s.IpAddress && r.GamePort == s.GamePort)));
            }

            return result;
        }) ?? Enumerable.Empty<ServerInfo>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _serverCancellationToken = cancellationToken;
        var listenTask = ListenAsync(cancellationToken);
        var connectToGameServersTask = ReconnectToServersAsync(cancellationToken);
        await listenTask;
    }

    private async Task ReconnectToServersAsync(CancellationToken cancellationToken)
    {
        try
        {
            _serverReconnectCancellationTokenSource?.Cancel();
            var serverInfos = await GetServerInfoAsync();
            _serverReconnectCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await Task.WhenAll(serverInfos.Select(serverInfo => RetryConnectionAsync(GetIpAddress(), _port, serverInfo, _serverReconnectCancellationTokenSource.Token)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to reconnect to servers");
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var server = StartTcpListener();
        _logger.LogInformation("Server started");
        var tasks = new List<Task>();

        while (server.Server.IsBound && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await server.AcceptTcpClientAsync(cancellationToken);
                AppDiagnostics.ConnectedClientsCounter.Add(1);

                var serverInfo = await GetServerInfoAsync();
                var ipEndPoint = GetIpEndPoint(client);
                var ipv4 = ipEndPoint.Address.MapToIPv4();
                var isPrivateIp = ipv4.IsPrivate();
                if (serverInfo.All(s => s.IpAddress != ipv4.ToString()) && !isPrivateIp)
                {
                    _logger.LogWarning("Unauthorized connection from {IpAddress} - closing socket", ipEndPoint.Address);
                    client.Close();
                    AppDiagnostics.RejectedClientsCounter.Add(1);
                    continue;
                }

                AppDiagnostics.AcceptedClientsCounter.Add(1);
                _connections.TryAdd(ipEndPoint, client);

                var task = HandleConnectionLegacyAsync(client, cancellationToken);

                //var task = HandleConnectionAsync(client, cancellationToken);
                tasks.Add(task);
            }
            catch (TaskCanceledException)
            {
                // Server is shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game server TCP error");
            }
        }

        if (cancellationToken.IsCancellationRequested)
            server?.Server?.Close();

        _logger.LogInformation("Server stopped");

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handle connection error");
        }
    }

    private TcpListener StartTcpListener()
    {
        var server = new TcpListener(IPAddress.Any, _port);
        server.Start();
        return server;
    }

    private async Task HandleConnectionLegacyAsync(TcpClient client, CancellationToken parentCancellationToken)
    {
        var ipEndPoint = GetIpEndPoint(client);
        _logger.LogInformation("Client {IpAddress}:{Port} connected", ipEndPoint.Address, ipEndPoint.Port);

        // TODO: Timeout before connection is dropped if it doesn't send serverInfo event?

        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken);
        var cancellationToken = cancellationTokenSource.Token;
        IGameServer gameServer = null;

        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream);

            await using var writer = new BinaryWriter(stream);
            IGameWriter gameWriter = new GameWriter(writer, _logSend, _globalServices.GetRequiredService<ILogger<GameWriter>>(), cancellationToken);
            IGameReader gameReader = null;

            while (client.Connected && stream.CanRead && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // TODO: use pipe
                    // TODO: switch to .ReadLineAsync(cancellationToken) in .NET 7
                    var message = await reader.ReadLineAsync().WaitAsync(cancellationToken);
                    if (message == null)
                    {
                        // BF2 server returns NULL when disconnecting
                        // Happens when restarting or connecting to a different instance
                        _logger.LogError("Null message received for {ServerId}", gameServer?.Id);
                        break;
                    }

                    AppDiagnostics.ReceivedMessagesCounter.Add(1);

                    // TODO: temp ignore spam
                    //if (!message.StartsWith("playerPos") && !message.StartsWith("projectilePos"))
                    //    Logger.LogDebug($"recv: {message}");

                    if (_logRecv)
                    {
                        _logger.LogDebug("recv: {Message}", message);
                    }

                    if (gameServer == null || gameReader == null)
                    {
                        // Server is currently pending - wait for serverInfo event so we can get id
                        var result = await GetGameServerFromInitialMessageAsync(message, ipEndPoint, gameWriter, client, cancellationTokenSource);
                        gameServer = result.GameServer;
                        gameReader = result.GameReader;
                    }

                    if (gameReader != null)
                    {
                        gameReader.QueueMessage(message);

                        //await gameReader?.ParseMessageAsync(message);
                    }

                    //if (!message.Contains("255"))
                    //    gameReader.ParseMessage(message);
                    //else
                    //    Logger.LogDebug($"RECV 255: {message}");
                }
                catch (IOException ex) when (ex.InnerException is SocketException)
                {
                    // Disconnected
                    _logger.LogDebug("Disconnected");
                }
                catch (NotImplementedException)
                {
                    //Logger.LogDebug("Not implemented!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while handling server message");
                    AppDiagnostics.ErrorMessagesCounter.Add(1);
                }
            }

            // Make sure this connection is closed if a cancellation was requested
            client.Close();

            if (gameServer != null)
                await gameServer.UpdateSocketStateAsync(SocketState.Disconnected, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling connection");
        }

        _connections.TryRemove(ipEndPoint, out var removed);
        _logger.LogInformation("Client {IpAddress}:{Port} disconnected", ipEndPoint.Address, ipEndPoint.Port);

        var serverInfo = await GetServerInfoAsync();
        var ipv4 = gameServer?.IpAddress.MapToIPv4();
        var matchingServerInfo = serverInfo.FirstOrDefault(s => s.IpAddress == ipv4?.ToString() && s.GamePort == gameServer?.GamePort);
        if (matchingServerInfo == null)
        {
            _logger.LogError("Unable to find matching server info for {IpAddress}:{Port}", ipv4, gameServer?.GamePort);
            return;
        }

        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        await RetryConnectionAsync(_ipAddress, _port, matchingServerInfo, _serverReconnectCancellationTokenSource?.Token ?? cancellationToken);
    }

    //private async Task HandleConnectionAsync(TcpClient client)
    //{
    //    var ipEndPoint = GetIpEndPoint(client);

    //    Log.Information("Client {IpAddress}:{Port} connected", ipEndPoint.Address, ipEndPoint.Port);

    //    // TODO: Timeout before connection is dropped if it doesn't send serverInfo event?

    //    IGameServer gameServer = null;

    //    try
    //    {
    //        await using var stream = client.GetStream();
    //        //using var reader = new StreamReader(stream);

    //        var reader = PipeReader.Create(stream);
    //        //var writer = PipeWriter.Create(stream);
    //        await using var writer = new BinaryWriter(stream);
    //        IGameWriter gameWriter = new GameWriter(writer, _logSend);
    //        IGameReader gameReader = null;
    //        //gameServer = await GetGameServerAsync(ipEndPoint, gameWriter);
    //        //IGameReader gameReader = new GameReader(gameServer);

    //        // Log all game events
    //        //IGameReader gameReader = new GameReader(gameServer, $"{Path.Combine(AppContext.BaseDirectory, $"gameevents-{gameServer.Id.Replace(".", "-").Replace(":", "-")}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.txt")}");

    //        //while (client.Connected && stream.CanRead)
    //        while (true)
    //        {
    //            try
    //            {
    //                var result = await reader.ReadAsync();
    //                var buffer = result.Buffer;

    //                ProcessLines(ref buffer, ref gameServer, ref gameReader);

    //                reader.AdvanceTo(buffer.Start, buffer.End);

    //                if (result.IsCompleted)
    //                    break;

    //                //// TODO: use pipe
    //                ////var message = await reader.ReadLineAsync();
    //                //if (message == null)
    //                //{
    //                //    // BF2 server returns NULL when disconnecting
    //                //    // Happens when restarting or connecting to a different instance
    //                //    Log.Error("Null message received");
    //                //    break;
    //                //}

    //                //// TODO: temp ignore spam
    //                ////if (!message.StartsWith("playerPos") && !message.StartsWith("projectilePos"))
    //                ////    Logger.LogDebug($"recv: {message}");

    //                //if (_logRecv)
    //                //{
    //                //    Log.Debug("recv: {Message}", message);
    //                //}

    //                //if (gameServer == null || gameReader == null)
    //                //{
    //                //    // Server is currently pending - wait for serverInfo event so we can get id
    //                //    var result = await GetGameServerFromInitialMessageAsync(message, ipEndPoint, gameWriter);
    //                //    gameServer = result.GameServer;
    //                //    gameReader = result.GameReader;
    //                //}

    //                //if (gameReader != null)
    //                //{
    //                //    gameReader.QueueMessage(message);
    //                //    //await gameReader?.ParseMessageAsync(message);
    //                //}

    //                ////if (!message.Contains("255"))
    //                ////    gameReader.ParseMessage(message);
    //                ////else
    //                ////    Logger.LogDebug($"RECV 255: {message}");
    //            }
    //            catch (IOException ex) when (ex.InnerException is SocketException)
    //            {
    //                // Disconnected
    //                Log.Debug("Disconnected");
    //            }
    //            catch (NotImplementedException)
    //            {
    //                //Logger.LogDebug("Not implemented!");
    //            }
    //            catch (Exception ex)
    //            {
    //                Log.Error(ex, "Error while handling server message");
    //            }
    //        }

    //        if (gameServer != null)
    //            await gameServer.UpdateSocketStateAsync(SocketState.Disconnected);
    //    }
    //    catch (Exception ex)
    //    {
    //        Log.Error(ex, "Error while handling connection");
    //    }

    //    _connections.TryRemove(ipEndPoint, out var removed);
    //    Log.Information("Client {IpAddress}:{Port} disconnected", ipEndPoint.Address, ipEndPoint.Port);

    //    var ipv4 = gameServer?.IpAddress.MapToIPv4();
    //    var matchingServerInfo = _serverInfo.FirstOrDefault(s => s.IpAddress == ipv4?.ToString() && s.GamePort == gameServer?.GamePort);
    //    if (matchingServerInfo == null)
    //    {
    //        Log.Error("Unable to find matching server info for {IpAddress}:{Port}", ipv4, gameServer?.GamePort);
    //        return;
    //    }

    //    await Task.Delay(TimeSpan.FromMinutes(1));
    //    await RetryConnectionAsync(_ipAddress, _port, matchingServerInfo);
    //}

    //private void ProcessLines(ref ReadOnlySequence<byte> buffer, ref IGameServer gameServer, ref IGameReader gameReader)
    //{
    //    string str = null;

    //    if (buffer.IsSingleSegment)
    //    {
    //        var span = buffer.FirstSpan;
    //        int consumed;
    //        while (span.Length > 0)
    //        {
    //            var newLine = span.IndexOf(NewLine);

    //            if (newLine == -1)
    //                break;

    //            var line = span.Slice(0, newLine);
    //            var message = Encoding.UTF8.GetString(line);
    //            HandleMessage(message, ref gameServer, ref gameReader);

    //            //// TODO simulate string processing
    //            //str = str.AsSpan().Slice(0, 5).ToString();

    //            consumed = line.Length + NewLine.Length;
    //            span = span.Slice(consumed);
    //            buffer = buffer.Slice(consumed);
    //        }
    //    }
    //    else
    //    {
    //        var sequenceReader = new SequenceReader<byte>(buffer);

    //        while (!sequenceReader.End)
    //        {
    //            while (sequenceReader.TryReadTo(out ReadOnlySpan<byte> line, NewLine))
    //            {
    //                var message = Encoding.UTF8.GetString(line);
    //                HandleMessage(message, ref gameServer, ref gameReader);

    //                //// TODO simulate string processing
    //                //str = str.AsSpan().Slice(0, 5).ToString();
    //            }

    //            buffer = buffer.Slice(sequenceReader.Position);
    //            sequenceReader.Advance(buffer.Length);
    //        }
    //    }
    //}

    //private void HandleMessage(string message, ref IGameServer gameServer, ref IGameReader gameReader)
    //{
    //    //// TODO: use pipe
    //    ////var message = await reader.ReadLineAsync();
    //    //if (message == null)
    //    //{
    //    //    // BF2 server returns NULL when disconnecting
    //    //    // Happens when restarting or connecting to a different instance
    //    //    Log.Error("Null message received");
    //    //    break;
    //    //}

    //    //// TODO: temp ignore spam
    //    ////if (!message.StartsWith("playerPos") && !message.StartsWith("projectilePos"))
    //    ////    Logger.LogDebug($"recv: {message}");

    //    if (_logRecv)
    //    {
    //        Log.Debug("recv: {Message}", message);
    //    }

    //    if (gameServer == null || gameReader == null)
    //    {
    //        // Server is currently pending - wait for serverInfo event so we can get id
    //        var result = GetGameServerFromInitialMessageAsync(message, ipEndPoint, gameWriter).GetAwaiter().GetResult();
    //        //var result = await GetGameServerFromInitialMessageAsync(message, ipEndPoint, gameWriter);
    //        gameServer = result.GameServer;
    //        gameReader = result.GameReader;
    //    }

    //    if (gameReader != null)
    //    {
    //        gameReader.QueueMessage(message);
    //        //await gameReader?.ParseMessageAsync(message);
    //    }

    //    //if (!message.Contains("255"))
    //    //    gameReader.ParseMessage(message);
    //    //else
    //    //    Logger.LogDebug($"RECV 255: {message}");
    //}

    private static IPEndPoint GetIpEndPoint(TcpClient client)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint result)
            throw new InvalidCastException();

        return result;
    }

    private async Task<(IGameServer? GameServer, IGameReader? GameReader)> GetGameServerFromInitialMessageAsync(
        string message,
        IPEndPoint ipEndPoint,
        IGameWriter gameWriter,
        TcpClient tcpClient,
        CancellationTokenSource cancellationTokenSource
    )
    {
        var parts = message.Split('\t');
        var eventType = parts[0];
        if (eventType != "serverInfo")
        {
            _logger.LogWarning("Unexpected server event {EventType} - expected serverInfo", eventType);
            return default;
        }

        // Find ServerInfo - Order by exact IP match, since we can accept all private network IPs when using containers
        var serverInfos = await GetServerInfoAsync();
        var gamePort = int.Parse(parts[3]);
        var serverInfo = serverInfos
            .OrderByDescending(i => i.IpAddress == ipEndPoint.Address.ToString())
            .FirstOrDefault(i =>
                (i.IpAddress == ipEndPoint.Address.ToString() || ipEndPoint.Address.IsPrivate() || ipEndPoint.Address.Equals(_ipAddress)) &&
                i.GamePort == gamePort
            );

        if (serverInfo == null)
            throw new Exception($"Server info not found in settings: {ipEndPoint.Address}:{gamePort}");

        var publicIpAddress = ipEndPoint.Address.IsPrivate() ? _ipAddress : ipEndPoint.Address;
        var key = $"{publicIpAddress}:{gamePort}";
        var gameServer = await GetGameServerAsync();

        // var gameServer = await GetGameServerAsync(publicIpAddress, ipEndPoint.Address, gameWriter, key, serverInfo, tcpClient, cancellationToken);
        var gameReader = new GameReader(gameServer, _globalServices.GetRequiredService<ILogger<GameReader>>(), cancellationToken: cancellationTokenSource.Token);
        gameServer.GameReader = gameReader;
        return (gameServer, gameReader);

        async Task<IGameServer> GetGameServerAsync()
        {
            if (_servers.TryGetValue(key, out var existingContext))
            {
                _logger.LogInformation("Reused existing GameServer instance {ServerId}", existingContext.GameServer.Id);
                await existingContext.GameServer.SetReconnectedAsync(gameWriter, DateTimeOffset.UtcNow);
                return existingContext.GameServer;
            }

            _logger.LogInformation("Created new GameServer instance {ServerKey}", key);
            var newServer = await GameServer.CreateAsync(publicIpAddress, ipEndPoint.Address, gameWriter, serverInfo, _globalServices, cancellationTokenSource.Token);
            var context = new GameServerConnectionContext(key, newServer, tcpClient, cancellationTokenSource);
            _servers.TryAdd(key, context);
            return newServer;
        }
    }

    private Task RetryConnectionAsync(IPAddress ipAddress, int port, ServerInfo serverInfo, CancellationToken cancellationToken)
    {
        return Policy.Handle<Exception>()
            .WaitAndRetryForeverAsync(
                sleepDurationProvider: retryAttempt => retryAttempt < 10 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10),
                onRetry: (exception, retryAttempt, timespan) =>
                {
                    if (exception.Message.Contains("Connection refused") || exception.Message.Contains("failed to respond"))
                    {
                        _logger.LogWarning(
                            "Failed attempt {RetryAttempt} for {ServerIpAddress}:{ServerGamePort}. Retrying in {Timespan} ({ExceptionMessage})",
                            retryAttempt, serverInfo.IpAddress, serverInfo.GamePort, timespan, exception.Message
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            exception,
                            "Failed attempt {RetryAttempt} for {ServerIpAddress}:{ServerGamePort}. Retrying in {Timespan}",
                            retryAttempt, serverInfo.IpAddress, serverInfo.GamePort, timespan
                        );
                    }
                }
            )
            .ExecuteAsync(async (_) =>
            {
                var resolvedIpAddress = await serverInfo.IpAddress.GetIpAddressAsync();
                var gameServerIps = new List<IPAddress> { resolvedIpAddress };
                if (resolvedIpAddress.IsPrivate())
                {
                    // If the first IP doesn't work we can try the public IP
                    // NOTE: This could cause problems when the container port is not the same as the public port
                    // In that case it could lead to a reconnect loop if it leads to another server on the host
                    gameServerIps.Add(ipAddress);
                }

                var exceptions = new List<Exception>();
                foreach (var gameServerIp in gameServerIps)
                {
                    AppDiagnostics.ServerConnectRetryCounter.Add(1, new KeyValuePair<string, object?>("server-address", $"{ipAddress}:{port}"));

                    try
                    {
                        // Check if server has already connected
                        var matchingServer = _servers.Values.FirstOrDefault(s =>
                                (s.GameServer.IpAddress.Equals(gameServerIp) || gameServerIp.IsPrivate() && s.GameServer.IpAddress.Equals(ipAddress)) && s.GameServer.GamePort == serverInfo.GamePort
                            )
                            ?.GameServer;

                        if (matchingServer?.SocketState == SocketState.Connected)
                        {
                            _logger.LogInformation("Server {ServerId} is already connected. Aborting connection retries", matchingServer.Id);
                            return;
                        }

                        using var client = new RconClient(gameServerIp, serverInfo.RconPort, serverInfo.RconPassword, _globalServices.GetRequiredService<ILogger<RconClient>>());

                        // Let mm_webadmin connect to this server using the IP from the RCON connection
                        // Used in most cases, unless connection fails during reconnection request
                        _logger.LogInformation("Sending reconnect command for {GameServerIp} to connect to this server", gameServerIp);
                        var response = await client.SendAsync($"wa connectprivate {port}");
                        if (response?.Contains("Connected successfully") ?? false)
                            return;

                        if (response?.Contains("Connection failed") ?? false)
                        {
                            // Let mm_webadmin connect to a server with a specific IP
                            // Used when the connecting IP is not open for connections the other way, but another IP for the same machine is (local dev)
                            _logger.LogInformation("Sending reconnect command for {GameServerIp} to connect to {SelfIpAddress}", gameServerIp, ipAddress);
                            response = await client.SendAsync($"wa connect {ipAddress} {port}");
                            if (response?.Contains("Connected successfully") ?? false)
                                return;
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                // Only throw if all attempts failed
                if (exceptions.Count == gameServerIps.Count)
                    throw exceptions.First();
            }, cancellationToken);
    }

    public async Task AddOrUpdateServerAsync(Data.Entities.Server server)
    {
        using var serviceScope = _globalServices.CreateScope();
        var serverRepository = serviceScope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();
        await serverRepository.SetServerAsync(server);
        await HandleServerUpdateAsync(server.ServerId, ServerUpdateType.AddOrUpdate);
    }

    public async Task RemoveServerAsync(string serverId)
    {
        using var serviceScope = _globalServices.CreateScope();
        var serverRepository = serviceScope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();
        await serverRepository.RemoveServerAsync(serverId);
        await HandleServerUpdateAsync(serverId, ServerUpdateType.Remove);
    }

    private async Task HandleServerUpdateAsync(string serverId, ServerUpdateType type)
    {
        _logger.LogDebug("Handling server {UpdateType} for {ServerId}", type, serverId);

        // Clear cached server infos
        _cache.Remove(nameof(GetServerInfoAsync));

        if (_servers.TryRemove(serverId, out var serverContext))
            serverContext?.CancellationTokenSource.Cancel();

        // Setup new reconnection tasks, since servers could have been added or removed
        _ = Task.Run(async () =>
        {
            try
            {
                await ReconnectToServersAsync(_serverCancellationToken ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to servers");
            }
        }, _serverCancellationToken ?? CancellationToken.None);
    }

    private record GameServerConnectionContext(string ServerId, GameServer GameServer, TcpClient TcpClient, CancellationTokenSource CancellationTokenSource);
}

public enum ServerUpdateType
{
    AddOrUpdate,
    Remove
}