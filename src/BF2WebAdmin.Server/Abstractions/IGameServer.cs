using System.Net;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Communication.DTOs;

namespace BF2WebAdmin.Server.Abstractions;

/// <summary>
/// The game server as modules see it: state, the per-server mediator and the command writer.
/// The state is fed only by the admin channel (<see cref="AdminChannel.AdminStateApplier"/>).
/// </summary>
public interface IGameServer
{
    DateTime StartTime { get; }

    IModManager ModManager { get; }
    IGameWriter GameWriter { get; }

    /// <summary>Game scripts on this server, where its module runs them (docs/scripts.md).</summary>
    IScriptRunner Scripts { get; }

    ServerInfo ServerInfo { get; }

    /// <summary>The configured server id (Server.ServerId), also the module's adminServerId.</summary>
    string Id { get; }
    string Name { get; }
    IPAddress IpAddress { get; }
    IPAddress ConnectedIpAddress { get; }
    int GamePort { get; }
    int QueryPort { get; }
    int MaxPlayers { get; }
    GameState State { get; }
    SocketState SocketState { get; }

    Map? Map { get; }
    IEnumerable<Map> Maps { get; }
    IEnumerable<Team> Teams { get; }
    IEnumerable<Player> Players { get; }
    IEnumerable<Vehicle> Vehicles { get; }
    IEnumerable<Projectile> Projectiles { get; }
    IEnumerable<(MessageDto Message, DateTimeOffset Time)> Messages { get; }
    IEnumerable<(string Message, DateTimeOffset Time)> Events { get; }
    ValueTask CreateModManagerAsync(bool forceReinitialize = false);
    Player? GetPlayer(int index);
    Player? GetPlayerByName(string namePart);
    Player? GetPlayerByHash(string playerHash);

    /// <summary>Write every message received on the admin channel to a file, until stopped.</summary>
    void StartRecording(string path);
    void StopRecording();
}
