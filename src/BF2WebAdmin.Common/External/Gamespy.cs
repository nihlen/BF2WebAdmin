// ReSharper disable All
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Nihlen.Gamespy;

public static class AsyncExtensions
{
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        // Useful for classes like UdpClient which have no timeouts built-in
        var tcs = new TaskCompletionSource<bool>();
        await using (cancellationToken.Register(s => (s as TaskCompletionSource<bool>)?.TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        return task.Result;
    }
}

public static class DictionaryExtensions
{
    public static string GetValue(this Dictionary<string, string> dict, string key)
    {
        return dict.ContainsKey(key) ? dict[key] : null;
    }
}

public class GamespyReader
{
    private readonly byte[] _readBuffer;
    private int _offset;

    public bool HasData => _offset < _readBuffer.Length;

    public GamespyReader(byte[] buffer)
    {
        _readBuffer = buffer;
    }

    public byte ReadByte()
    {
        if (_offset == _readBuffer.Length)
        {
            return 0;
        }

        return _readBuffer[_offset++];
    }

    public byte PeekByte()
    {
        if (_offset == _readBuffer.Length)
        {
            return 0;
        }

        return _readBuffer[_offset];
    }

    public string ReadNextParam()
    {
        var sb = new StringBuilder();
        for (; _offset < _readBuffer.Length; _offset++)
        {
            if (_readBuffer[_offset] == 0)
            {
                _offset++;
                break;
            }

            sb.Append((char)_readBuffer[_offset]);
        }

        return sb.ToString();
    }
}

public interface IGamespyService
{
    Task<ServerInfo> QueryServerAsync(IPAddress ipAddress, int queryPort);
}

public class Gamespy3Service : IGamespyService
{
    // Reference: https://bf2tech.uturista.pt/index.php/GameSpy_Protocol
    public async Task<ServerInfo> QueryServerAsync(IPAddress ipAddress, int queryPort)
    {
        var endpoint = new IPEndPoint(ipAddress, queryPort);
        using var client = new UdpClient();

        client.Connect(endpoint);
        if (!client.Client.Connected)
            throw new Exception($"Could not connect to {ipAddress}:{queryPort} (UDP)");

        var stopWatch = Stopwatch.StartNew();
        var bytes = new byte[] { 0xFE, 0xFD, 0x00, 0x10, 0x20, 0x30, 0x40, 0xFF, 0xFF, 0xFF, 0x01 };
        var sendCancellationToken = new CancellationTokenSource(2_500);
        await client.SendAsync(bytes, bytes.Length).WithCancellation(sendCancellationToken.Token);

        var messages = new List<byte[]>();

        var isMessageComplete = false;
        var receiveCancellationToken = new CancellationTokenSource(2_500);
        while (!isMessageComplete && !receiveCancellationToken.IsCancellationRequested)
        {
            var result = await client.ReceiveAsync().WithCancellation(receiveCancellationToken.Token);
            messages.Add(result.Buffer);
            isMessageComplete = (result.Buffer[14] & (1 << 7)) != 0;
        }

        var elapsedTime = stopWatch.ElapsedMilliseconds;

        if (!isMessageComplete)
            throw new OperationCanceledException($"Server receive was cancelled, but did not receive last message: {client.Available}");

        const int headerLength = 15;
        var payloads = messages.Select(m => m.Skip(headerLength).Take(m.Length - headerLength).ToArray());

        try
        {
            var parsedServer = Parse(payloads);
            var serverInfo = ServerInfo.Create(ipAddress, queryPort, parsedServer.ServerData, parsedServer.PlayerData, parsedServer.TeamData);
            serverInfo.ResponseTime = elapsedTime;
            return serverInfo;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to parse server response from {ipAddress}: {Encoding.UTF8.GetString(messages.SelectMany(m => m.Skip(headerLength).Take(m.Length - headerLength)).ToArray())}", e);
        }
    }

    public static ParsedServer Parse(IEnumerable<byte[]> payloads)
    {
        var serverData = new Dictionary<string, string>();
        var playerData = new Dictionary<string, IList<string>>();
        var teamData = new Dictionary<string, IList<string>>();

        foreach (var payload in payloads)
        {
            var reader = new GamespyReader(payload);
            while (reader.HasData)
            {
                var type = reader.ReadByte();
                if (type == 0)
                {
                    ParseServerInformation(reader);
                }
                else if (type == 1)
                {
                    ParsePlayerInformation(reader);
                }
                else if (type == 2)
                {
                    ParseTeamInformation(reader);
                }
            }
        }

        void ParseServerInformation(GamespyReader reader)
        {
            while (reader.PeekByte() != 0)
            {
                var key = reader.ReadNextParam();
                var value = reader.ReadNextParam();
                serverData.Add(key, value);
            }

            reader.ReadByte();
        }

        void ParsePlayerInformation(GamespyReader reader)
        {
            while (reader.PeekByte() != 0)
            {
                var key = reader.ReadNextParam();
                if (!playerData.ContainsKey(key))
                {
                    playerData.Add(key, new List<string>());
                }

                var offset = reader.ReadByte();

                while (reader.PeekByte() != 0)
                {
                    if (playerData[key].Count <= offset)
                    {
                        playerData[key].Add(string.Empty);
                    }

                    playerData[key][offset++] = reader.ReadNextParam();
                }

                reader.ReadByte();
            }

            reader.ReadByte();
        }

        void ParseTeamInformation(GamespyReader reader)
        {
            while (reader.PeekByte() != 0)
            {
                var key = reader.ReadNextParam();
                if (!teamData.ContainsKey(key))
                {
                    teamData.Add(key, new List<string>());
                }

                var offset = reader.ReadByte();

                while (reader.PeekByte() != 0)
                {
                    if (teamData[key].Count <= offset)
                    {
                        teamData[key].Add(string.Empty);
                    }

                    teamData[key][offset++] = reader.ReadNextParam();
                }

                reader.ReadByte();
            }

            reader.ReadByte();
        }

        return new ParsedServer
        {
            ServerData = serverData,
            PlayerData = playerData,
            TeamData = teamData
        };
    }

    public class ParsedServer
    {
        public Dictionary<string, string> ServerData { get; set; }
        public Dictionary<string, IList<string>> PlayerData { get; set; }
        public Dictionary<string, IList<string>> TeamData { get; set; }
    }
}

//public class ServerModel
//{
//    public bool IsOnline { get; set; }
//    public string Name { get; set; }
//    public string GameName { get; set; }
//    public string MapName { get; set; }
//    public int NumPlayers { get; set; }
//    public int MaxPlayers { get; set; }
//    public string GameMode { get; set; }
//    public bool HasPassword { get; set; }
//    public string HostIp { get; set; }
//    public int HostPort { get; set; }
//    public int QueryPort { get; set; }

//    // bf2_
//    public bool IsDedicated { get; set; }
//    public bool IsRanked { get; set; }
//    public bool HasAntiCheat { get; set; }
//    public string OperatingSystem { get; set; }
//    public bool HasAutoRecord { get; set; }
//    public string DemoIndexUri { get; set; }
//    public bool HasVoip { get; set; }
//    public string SponsorText { get; set; }
//    public string CommunityLogoUri { get; set; }
//    public string Team1 { get; set; }
//    public string Team2 { get; set; }
//    public int NumBots { get; set; }
//    public int MapSize { get; set; }
//    public bool HasGlobalUnlocks { get; set; }
//    public int ReservedSlots { get; set; }
//    public bool HasNoVehicles { get; set; }

//    public Player[] Players { get; set; }

//    public long ResponseTime { get; set; }
//    public string CountryCode { get; set; }

//    public ServerModel(string uri, bool isOnline)
//    {
//        Name = uri;
//        IsOnline = isOnline;
//        Players = new Player[0];

//        try
//        {
//            var parts = uri.Split(':');
//            HostIp = parts[0];
//            var queryPort = 0;
//            int.TryParse(parts[1], out queryPort);
//            QueryPort = queryPort;
//        }
//        catch (Exception)
//        {
//            // Ignore
//        }
//    }

//    public ServerModel(ServerInfo info)
//    {
//        IsOnline = info.IsOnline;
//        Name = info.Name;
//        QueryPort = info.QueryPort;
//        HasGlobalUnlocks = info.HasGlobalUnlocks;
//        ReservedSlots = info.ReservedSlots;
//        MapSize = info.MapSize;
//        HasAutoRecord = info.HasAutoRecord;
//        IsRanked = info.IsRanked;
//        HasAntiCheat = info.HasAntiCheat;
//        HasNoVehicles = info.HasNoVehicles;
//        NumPlayers = info.NumPlayers;
//        MapName = info.MapName;
//        NumBots = info.NumBots;
//        DemoIndexUri = info.DemoIndexUri?.ToString();
//        IsDedicated = info.IsDedicated;
//        HasVoip = info.HasVoip;
//        SponsorText = info.SponsorText;
//        ResponseTime = info.ResponseTime;
//        CommunityLogoUri = info.CommunityLogoUri?.ToString();
//        Team1 = info.Team1;
//        Team2 = info.Team2;
//        OperatingSystem = info.OperatingSystem;
//        GameMode = info.GameMode;
//        GameName = info.GameName;
//        HasPassword = info.HasPassword;
//        HostIp = info.HostIp?.ToString();
//        HostPort = info.HostPort;
//        MaxPlayers = info.MaxPlayers;
//        Players = info.Players;
//    }
//}

public class ServerInfo
{
    public bool IsOnline { get; set; }
    public string Name { get; set; }
    public string GameName { get; set; }
    public string GameVersion { get; set; }
    public string MapName { get; set; }
    public string GameType { get; set; }
    public string GameVariant { get; set; }
    public int NumPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public string GameMode { get; set; }
    public bool HasPassword { get; set; }
    public int TimeLimit { get; set; }
    public int RoundTime { get; set; }
    public IPAddress HostIp { get; set; }
    public int HostPort { get; set; }
    public int QueryPort { get; set; }
    public bool IsDedicated { get; set; }
    public bool IsRanked { get; set; }
    public bool HasAntiCheat { get; set; }
    public string OperatingSystem { get; set; }
    public bool HasAutoRecord { get; set; }
    public Uri DemoIndexUri { get; set; }
    public Uri DemoDownloadUri { get; set; }
    public bool HasVoip { get; set; }
    public bool IsAutobalanced { get; set; }
    public bool HasFriendlyFire { get; set; }
    public string TeamkillMode { get; set; }
    public int StartDelay { get; set; }
    public double SpawnTime { get; set; }
    public string SponsorText { get; set; }
    public Uri SponsorLogoUri { get; set; }
    public Uri CommunityLogoUri { get; set; }
    public int ScoreLimit { get; set; }
    public int TicketRatio { get; set; }
    public double TeamRatio { get; set; }
    public string Team1 { get; set; }
    public string Team2 { get; set; }
    public int NumBots { get; set; }
    public bool IsPure { get; set; }
    public int MapSize { get; set; }
    public bool HasGlobalUnlocks { get; set; }
    public double Fps { get; set; }
    public int ReservedSlots { get; set; }
    public bool HasNoVehicles { get; set; }

    public Player[] Players { get; set; } = Array.Empty<Player>();

    public long ResponseTime { get; set; }

    public static ServerInfo Create(IPAddress ipAddress, int queryPort, Dictionary<string, string> serverData, Dictionary<string, IList<string>> playerData, Dictionary<string, IList<string>> teamData)
    {
        var result = new ServerInfo
        {
            HostIp = ipAddress,
            QueryPort = queryPort
        };

        ApplyServerData(serverData, result);
        ApplyPlayerData(playerData, result);
        ApplyTeamData(teamData, result);

        return result;
    }

    private static void ApplyServerData(Dictionary<string, string> serverData, ServerInfo info)
    {
        info.IsOnline = true;
        info.Name = serverData.GetValue("hostname");
        info.GameName = serverData.GetValue("gamename");
        info.GameVersion = serverData.GetValue("gamever");
        info.MapName = serverData.GetValue("mapname");
        info.GameType = serverData.GetValue("gametype");
        info.GameVariant = serverData.GetValue("gamevariant");
        info.NumPlayers = ParseInt(serverData.GetValue("numplayers"));
        info.MaxPlayers = ParseInt(serverData.GetValue("maxplayers"));
        info.GameMode = serverData.GetValue("gamemode");
        info.HasPassword = ParseBoolean(serverData.GetValue("password"));
        info.TimeLimit = ParseInt(serverData.GetValue("timelimit"));
        info.RoundTime = ParseInt(serverData.GetValue("roundtime"));
        info.HostPort = ParseInt(serverData.GetValue("hostport"));
        info.IsDedicated = ParseBoolean(serverData.GetValue("bf2_dedicated"));
        info.IsRanked = ParseBoolean(serverData.GetValue("bf2_ranked"));
        info.HasAntiCheat = ParseBoolean(serverData.GetValue("bf2_anticheat"));
        info.OperatingSystem = serverData.GetValue("bf2_os");
        info.HasAutoRecord = ParseBoolean(serverData.GetValue("bf2_autorec"));
        info.DemoIndexUri = ParseUri(serverData.GetValue("bf2_d_idx"));
        info.DemoDownloadUri = ParseUri(serverData.GetValue("bf2_d_dl"));
        info.HasVoip = ParseBoolean(serverData.GetValue("bf2_voip"));
        info.IsAutobalanced = ParseBoolean(serverData.GetValue("bf2_autobalanced"));
        info.HasFriendlyFire = ParseBoolean(serverData.GetValue("bf2_friendlyfire"));
        info.TeamkillMode = serverData.GetValue("bf2_tkmode");
        info.StartDelay = ParseInt(serverData.GetValue("bf2_startdelay"));
        info.SpawnTime = ParseDouble(serverData.GetValue("bf2_spawntime"));
        info.SponsorText = serverData.GetValue("bf2_sponsortext");
        info.SponsorLogoUri = ParseUri(serverData.GetValue("bf2_sponsorlogo_url"));
        info.CommunityLogoUri = ParseUri(serverData.GetValue("bf2_communitylogo_url"));
        info.ScoreLimit = ParseInt(serverData.GetValue("bf2_scorelimit"));
        info.TicketRatio = ParseInt(serverData.GetValue("bf2_ticketratio"));
        info.TeamRatio = ParseDouble(serverData.GetValue("bf2_teamratio"));
        info.Team1 = serverData.GetValue("bf2_team1");
        info.Team2 = serverData.GetValue("bf2_team2");
        info.NumBots = ParseInt(serverData.GetValue("bf2_bots"));
        info.IsPure = ParseBoolean(serverData.GetValue("bf2_pure"));
        info.MapSize = ParseInt(serverData.GetValue("bf2_mapsize"));
        info.HasGlobalUnlocks = ParseBoolean(serverData.GetValue("bf2_globalunlocks"));
        info.Fps = ParseDouble(serverData.GetValue("bf2_fps"));
        info.ReservedSlots = ParseInt(serverData.GetValue("bf2_reservedslots"));
        info.HasNoVehicles = ParseBoolean(serverData.GetValue("bf2_novehicles"));
    }

    private static void ApplyPlayerData(Dictionary<string, IList<string>> playerData, ServerInfo serverInfo)
    {
        serverInfo.Players = new Player[playerData["player_"].Count];
        for (var i = 0; i < serverInfo.Players.Length; i++)
        {
            serverInfo.Players[i] = new Player
            {
                Name = playerData["player_"][i],
                Pid = ParseInt(playerData["pid_"][i]),
                TotalScore = ParseInt(playerData["score_"][i]),
                Team = ParseInt(playerData["team_"][i]),
                Kills = ParseInt(playerData["skill_"][i]),
                Deaths = ParseInt(playerData["deaths_"][i]),
                Ping = ParseInt(playerData["ping_"][i]),
                IsBot = ParseBoolean(playerData["AIBot_"][i])
            };
        }
    }

    private static void ApplyTeamData(Dictionary<string, IList<string>> teamData, ServerInfo serverInfo)
    {
        var teams = new Team[teamData["team_t"].Count];
        for (var i = 0; i < teams.Length; i++)
        {
            teams[i] = new Team
            {
                Name = teamData["team_t"][i],
                Score = ParseInt(teamData["score_t"][i]),
            };
        }
    }

    private static int ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        int.TryParse(value, out var parsedValue);
        return parsedValue;
    }

    private static double ParseDouble(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        // TODO: Temp fix for trailing zeros
        value = value.Contains(".") ? value.Split('.')[0] : value;

        double.TryParse(value, out var parsedValue);
        return parsedValue;
    }

    private static bool ParseBoolean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value == "1" || value.ToLowerInvariant() == "true";
    }

    private static Uri? ParseUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Uri.TryCreate(value, UriKind.Absolute, out var result) ? result : null;
    }

    public class Player
    {
        public string Name { get; set; }
        public int Pid { get; set; }
        public int Team { get; set; }
        public int TeamScore => TotalScore - 2 * Kills;
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int TotalScore { get; set; }
        public int Ping { get; set; }
        public bool IsBot { get; set; }
        public int Rank { get; set; }
        public string CountryCode { get; set; }
    }

    public class Team
    {
        public string Name { get; set; }
        public int Score { get; set; }
    }
}
