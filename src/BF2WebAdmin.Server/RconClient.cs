using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using BF2WebAdmin.Server.Abstractions;
using Nihlen.Common.Telemetry;

namespace BF2WebAdmin.Server;

public class RconClient : IRconClient, IDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly string _password;
    private readonly ILogger _logger;

    private TcpClient? _client;

    private NetworkStream? _stream;
    private StreamReader? _reader;
    private BinaryWriter? _writer;

    public RconClient(IPAddress ipAddress, int port, string password, ILogger logger)
    {
        _ipAddress = ipAddress;
        _port = port;
        _password = password;
        _logger = logger;
    }

    public async Task<string> SendAsync(string command) => await SendAsync(new[] { command });

    public async Task<string> SendAsync(IEnumerable<string> commands)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("SendRconCommand");
        activity?.SetTag("bf2wa.rcon-endpoint", $"{_ipAddress}:{_port}");
        // activity?.SetTag("bf2wa.rcon-command", string.Join("|", commands));

        _logger.LogDebug("RCON connecting to {IpAddress} {Port}", _ipAddress, _port);
        if (_client?.Connected != true)
            await AuthenticateClientAsync();

        activity?.AddEvent(new("Authenticated successfully", DateTimeOffset.UtcNow));

        // Send and and read command responses one at a time, otherwise only the first response is written
        var sb = new StringBuilder();
        foreach (var command in commands)
        {
            _writer?.Write(GetCommandBytes(command));
            var response = ReadCommandResponse(_stream!);
            sb.Append(response);
            _logger.LogDebug("RCON response: {Response}", response);
        }
        
        return sb.ToString();
    }

    private async Task AuthenticateClientAsync()
    {
        // Use IPv4 or BF2 RCON fails
        _client = new TcpClient(AddressFamily.InterNetwork);
        await _client.ConnectAsync(_ipAddress, _port);

        Activity.Current?.AddEvent(new("Connected", DateTimeOffset.UtcNow));

        _stream = _client.GetStream();
        _reader = new StreamReader(_stream);
        _writer = new BinaryWriter(_stream);

        var authenticated = false;
        while (!_reader.EndOfStream)
        {
            var msg = await _reader.ReadLineAsync();
            _logger.LogDebug("RCON read: {Message}", msg);
            if (string.IsNullOrWhiteSpace(msg))
                continue;

            if (msg.StartsWith(RconResponses.DigestSeedResponse))
            {
                var hash = GetMd5Hash(msg[17..], _password);
                _writer.Write(GetLoginBytes("login " + hash));
            }
            else if (msg.StartsWith(RconResponses.NotAuthenticatedResponse))
            {
                throw new RconException("Not authenticated");
            }
            else if (msg.StartsWith(RconResponses.AuthenticationFailedResponse))
            {
                throw new RconException("Authentication failed");
            }
            else if (msg.StartsWith(RconResponses.AuthenticationSuccessResponse))
            {
                _logger.LogInformation("RCON authenticated successfully");
                authenticated = true;
                break;
            }
        }

        if (!authenticated)
            throw new RconException("Failed to authenticate");
    }

    private static string GetMd5Hash(string seed, string password)
    {
        var hash = ComputeHash(seed, password);
        var hex = ToHexString(hash);
        return hex;
    }

    private static byte[] ComputeHash(string seed, string password)
    {
        var bytes = Encoding.UTF8.GetBytes(seed + password);
        return MD5.Create().ComputeHash(bytes);
    }

    private static string ToHexString(byte[] bytes)
    {
        var hex = BitConverter.ToString(bytes);
        return hex.Replace("-", "").ToLower();
    }

    private static byte[] GetLoginBytes(string command)
    {
        return Encoding.UTF8.GetBytes(command + "\n");
    }

    private static byte[] GetCommandBytes(string command)
    {
        return Encoding.UTF8.GetBytes("\x02" + command + "\n");
    }

    private static string ReadCommandResponse(Stream stream)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var ch = stream.ReadByte();
            if (ch is -1 or 4)
                return sb.ToString();

            sb.Append((char)ch);
        }
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
    }

    private class RconException : Exception
    {
        public RconException(string message) : base(message) { }
    }

    public static class RconResponses
    {
        public const string DigestSeedResponse = "### Digest seed: ";
        public const string NotAuthenticatedResponse = "error: not authenticated: you can only invoke 'login'";
        public const string AuthenticationFailedResponse = "Authentication failed.";
        public const string AuthenticationSuccessResponse = "Authentication successful, rcon ready.";
    }
}
