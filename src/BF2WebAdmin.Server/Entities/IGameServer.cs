using System;
using System.Collections.Generic;
using System.Net;
using BF2WebAdmin.Server.Entities.Game;

namespace BF2WebAdmin.Server.Entities
{
    public interface IGameServer
    {
        IModManager ModManager { get; }
        IGameWriter GameWriter { get; }

        string Id { get; }
        string Name { get; }
        IPAddress IpAddress { get; }
        int GamePort { get; }
        int QueryPort { get; }
        int RconPort { get; }
        int MaxPlayers { get; }
        GameState State { get; }

        IEnumerable<Map> Maps { get; }
        IEnumerable<Player> Players { get; }
        IEnumerable<Vehicle> Vehicles { get; }
        IEnumerable<Projectile> Projectiles { get; }
        IEnumerable<Message> Messages { get; }

        event Action<string, int, int, int> ServerUpdate;
        event Action<IEnumerable<Map>> MapsChanged;
        event Action<Player> PlayerJoin;
        event Action<Player> PlayerLeft;
        event Action<Message> ChatMessage;

        void UpdateServerInfo(string name, int gamePort, int queryPort, int maxPlayers);
        void UpdateMaps(IList<string> maps);
        void AddPlayer(Player player);
        void RemovePlayer(Player player);
        void AddMessage(Message message);
    }
}