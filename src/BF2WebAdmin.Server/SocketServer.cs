using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Shared;
using Polly;
using Serilog;

namespace BF2WebAdmin.Server;

public interface ISocketServer
{
    IPAddress GetIpAddress();
    IGameServer? GetGameServer(string serverId);
    IEnumerable<IGameServer> GetGameServers(string serverGroup);
    Task ListenAsync();
    Task RetryConnectionAsync(IPAddress ipAddress, int port, ServerInfo serverInfo);
}

public class SocketServer : ISocketServer
{
    private static ReadOnlySpan<byte> NewLine => new[] { (byte)'\n' };

    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly IEnumerable<ServerInfo> _serverInfo;
    private readonly IServiceProvider _globalServices;
    private readonly bool _logSend;
    private readonly bool _logRecv;
    private readonly ConcurrentDictionary<IPEndPoint, TcpClient> _connections;
    private readonly ConcurrentDictionary<string, IGameServer> _servers;

    public IPAddress GetIpAddress() => _ipAddress;
    public IGameServer? GetGameServer(string serverId) => _servers.TryGetValue(serverId, out var server) ? server : null;
    public IEnumerable<IGameServer> GetGameServers(string serverGroup) => _servers.Values.Where(s => s.ModManager?.ServerSettings?.ServerGroup == serverGroup).ToList();

    public SocketServer(IPAddress ipAddress, int port, IEnumerable<ServerInfo> serverInfo, IServiceProvider globalServices, bool logSend, bool logRecv)
    {
        _ipAddress = ipAddress;
        _port = port;
        _serverInfo = serverInfo;
        _globalServices = globalServices;
        _logSend = logSend;
        _logRecv = logRecv;
        _connections = new ConcurrentDictionary<IPEndPoint, TcpClient>();
        _servers = new ConcurrentDictionary<string, IGameServer>();
    }

    public async Task ListenAsync()
    {
        var server = StartTcpListener();
        Log.Information("Server started");
        var tasks = new List<Task>();

        while (server.Server.IsBound)
        {
            try
            {
                var client = await server.AcceptTcpClientAsync();
                var ipEndPoint = GetIpEndPoint(client);
                var ipv4 = ipEndPoint.Address.MapToIPv4();
                var isPrivateIp = ipv4.IsPrivate();
                if (_serverInfo.All(s => s.IpAddress != ipv4.ToString()) && !isPrivateIp)
                {
                    Log.Warning("Unauthorized connection from {IpAddress} - closing socket", ipEndPoint.Address);
                    client.Close();
                    continue;
                }

                _connections.TryAdd(ipEndPoint, client);

                var task = HandleConnectionLegacyAsync(client);
                //var task = HandleConnectionAsync(client);
                tasks.Add(task);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Game server TCP error");
            }
        }

        Log.Information("Server stopped");

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Handle connection error");
        }
    }

    private TcpListener StartTcpListener()
    {
        var server = new TcpListener(IPAddress.Any, _port);
        //var server = new TcpListener(_ipAddress, _port);
        server.Start();
        return server;
    }

    private async Task HandleConnectionLegacyAsync(TcpClient client)
    {
        var ipEndPoint = GetIpEndPoint(client);

        Log.Information("Client {IpAddress}:{Port} connected", ipEndPoint.Address, ipEndPoint.Port);

        // TODO: Timeout before connection is dropped if it doesn't send serverInfo event?

        IGameServer gameServer = null;

        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream);

            await using var writer = new BinaryWriter(stream);
            IGameWriter gameWriter = new GameWriter(writer, _logSend);
            IGameReader gameReader = null;

            while (client.Connected && stream.CanRead)
            {
                try
                {
                    // TODO: use pipe
                    var message = await reader.ReadLineAsync();
                    if (message == null)
                    {
                        // BF2 server returns NULL when disconnecting
                        // Happens when restarting or connecting to a different instance
                        Log.Error("Null message received");
                        break;
                    }

                    // TODO: temp ignore spam
                    //if (!message.StartsWith("playerPos") && !message.StartsWith("projectilePos"))
                    //    Logger.LogDebug($"recv: {message}");

                    if (_logRecv)
                    {
                        Log.Debug("recv: {Message}", message);
                    }

                    if (gameServer == null || gameReader == null)
                    {
                        // Server is currently pending - wait for serverInfo event so we can get id
                        var result = await GetGameServerFromInitialMessageAsync(message, ipEndPoint, gameWriter);
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
                    Log.Debug("Disconnected");
                }
                catch (NotImplementedException)
                {
                    //Logger.LogDebug("Not implemented!");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while handling server message");
                }
            }

            if (gameServer != null)
                await gameServer.UpdateSocketStateAsync(SocketState.Disconnected, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while handling connection");
        }

        _connections.TryRemove(ipEndPoint, out var removed);
        Log.Information("Client {IpAddress}:{Port} disconnected", ipEndPoint.Address, ipEndPoint.Port);

        var ipv4 = gameServer?.IpAddress.MapToIPv4();
        var matchingServerInfo = _serverInfo.FirstOrDefault(s => s.IpAddress == ipv4?.ToString() && s.GamePort == gameServer?.GamePort);
        if (matchingServerInfo == null)
        {
            Log.Error("Unable to find matching server info for {IpAddress}:{Port}", ipv4, gameServer?.GamePort);
            return;
        }

        await Task.Delay(TimeSpan.FromMinutes(1));
        await RetryConnectionAsync(_ipAddress, _port, matchingServerInfo);
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
        var result = client.Client.RemoteEndPoint as IPEndPoint;
        if (result == null)
            throw new InvalidCastException();

        return result;
    }

    private async Task<(IGameServer? GameServer, IGameReader? GameReader)> GetGameServerFromInitialMessageAsync(string message, IPEndPoint ipEndPoint, IGameWriter gameWriter)
    {
        var parts = message.Split('\t');
        var eventType = parts[0];
        if (eventType != "serverInfo")
        {
            Log.Warning("Unexpected server event {EventType} - expected serverInfo", eventType);
            return default;
        }

        var gamePort = int.Parse(parts[3]);
        var serverInfo = _serverInfo.FirstOrDefault(i => 
            (i.IpAddress == ipEndPoint.Address.ToString() || ipEndPoint.Address.IsPrivate() || ipEndPoint.Address.Equals(_ipAddress)) && 
            i.GamePort == gamePort
        );
        if (serverInfo == null)
            throw new Exception($"Server info not found in settings: {ipEndPoint.Address}:{gamePort}");

        var publicIpAddress = ipEndPoint.Address.IsPrivate() ? _ipAddress : ipEndPoint.Address;
        var key = $"{publicIpAddress}:{gamePort}";
        var gameServer = await GetGameServerAsync(publicIpAddress, ipEndPoint.Address, gameWriter, key, serverInfo);
        var gameReader = new GameReader(gameServer);
        return (gameServer, gameReader);
    }

    private async Task<IGameServer> GetGameServerAsync(IPAddress publicIpAddress, IPAddress connectedIpAddress, IGameWriter gameWriter, string key, ServerInfo serverInfo)
    {
        if (_servers.ContainsKey(key))
        {
            var reusedServer = _servers[key];
            Log.Information("Reused existing GameServer instance {ServerId}", reusedServer.Id);
            await reusedServer.SetReconnectedAsync(gameWriter, DateTimeOffset.UtcNow);
            return reusedServer;
        }

        Log.Information("Created new GameServer instance {ServerKey}", key);
        var newServer = await GameServer.CreateAsync(publicIpAddress, connectedIpAddress, gameWriter, serverInfo, _globalServices);
        _servers.TryAdd(key, newServer);
        return newServer;
    }

    public Task RetryConnectionAsync(IPAddress ipAddress, int port, ServerInfo serverInfo)
    {
        return Policy.Handle<Exception>()
            .WaitAndRetryForeverAsync(
                sleepDurationProvider: retryAttempt => retryAttempt < 10 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(10),
                onRetry: (exception, retryAttempt, timespan) =>
                {
                    if (exception.Message.Contains("Connection refused") || exception.Message.Contains("failed to respond"))
                    {
                        Log.Warning(
                            "Failed attempt {RetryAttempt} for {ServerIpAddress}:{ServerGamePort}. Retrying in {Timespan} ({ExceptionMessage})", 
                            retryAttempt, serverInfo.IpAddress, serverInfo.GamePort, timespan, exception.Message
                        );
                    }
                    else
                    {
                        Log.Warning(
                            exception,
                            "Failed attempt {RetryAttempt} for {ServerIpAddress}:{ServerGamePort}. Retrying in {Timespan}", 
                            retryAttempt, serverInfo.IpAddress, serverInfo.GamePort, timespan
                        );
                    }
                }
            ).ExecuteAsync(async () =>
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
                    try
                    {
                        // Check if server has already connected
                        var matchingServer = _servers.Values.FirstOrDefault(s => 
                            (s.IpAddress.Equals(gameServerIp) || gameServerIp.IsPrivate() && s.IpAddress.Equals(ipAddress)) && s.GamePort == serverInfo.GamePort
                        );
                            
                        if (matchingServer?.SocketState == SocketState.Connected)
                        {
                            Log.Information("Server {ServerId} is already connected. Aborting connection retries", matchingServer.Id);
                            return;
                        }

                        using var client = new RconClient(gameServerIp, serverInfo.RconPort, serverInfo.RconPassword);

                        // Let mm_webadmin connect to this server using the IP from the RCON connection
                        // Used in most cases, unless connection fails during reconnection request
                        Log.Information("Sending reconnect command for {GameServerIp} to connect to this server", gameServerIp);
                        var response = await client.SendAsync($"wa connectprivate {port}");
                        if (response?.Contains("Connected successfully") ?? false)
                            return;

                        if (response?.Contains("Connection failed") ?? false)
                        {   
                            // Let mm_webadmin connect to a server with a specific IP
                            // Used when the connecting IP is not open for connections the other way, but another IP for the same machine is (local dev)
                            Log.Information("Sending reconnect command for {GameServerIp} to connect to {SelfIpAddress}", gameServerIp, ipAddress);
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
            });
    }
}