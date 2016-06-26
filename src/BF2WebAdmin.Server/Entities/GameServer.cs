using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using BF2WebAdmin.Server.Entities.Game;
using log4net;
using log4net.Config;

namespace BF2WebAdmin.Server.Entities
{
    public class GameServer : IGameServer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private readonly IModManager _mm;
        //private readonly IGameWriter _writer;

        public IModManager ModManager { get; }
        public IGameWriter GameWriter { get; }

        public string Id => $"{IpAddress}:{GamePort}";
        public string Name { get; private set; }
        public IPAddress IpAddress { get; }
        public int GamePort { get; private set; }
        public int QueryPort { get; private set; }
        public int RconPort { get; private set; }
        public int MaxPlayers { get; private set; }

        public GameState State { get; private set; }

        public IEnumerable<Map> Maps => _maps.ToArray();
        private readonly IList<Map> _maps = new List<Map>();

        public IEnumerable<Player> Players => _players.ToArray();
        private readonly IList<Player> _players = new List<Player>();

        public IEnumerable<Vehicle> Vehicles => _vehicles.ToArray();
        private readonly IList<Vehicle> _vehicles = new List<Vehicle>();

        public IEnumerable<Projectile> Projectiles => _projectiles.ToArray();
        private readonly IList<Projectile> _projectiles = new List<Projectile>();

        public IEnumerable<Message> Messages => _chatBuffer.Get().OrderBy(x => x.Time);
        private readonly CircularBuffer<Message> _chatBuffer = new CircularBuffer<Message>(100);

        public event Action<string, int, int, int> ServerUpdate;
        public event Action<IEnumerable<Map>> MapsChanged;
        public event Action<Message> ChatMessage;
        public event Action<Player> PlayerJoin;
        public event Action<Player> PlayerLeft;

        public GameServer(IPAddress ipAddress, IGameWriter writer)
        {
            IpAddress = ipAddress;
            GameWriter = writer;
            ModManager = new ModManager(this);
        }

        public void UpdateServerInfo(string name, int gamePort, int queryPort, int maxPlayers)
        {
            Name = name;
            GamePort = gamePort;
            QueryPort = queryPort;
            MaxPlayers = maxPlayers;
            ServerUpdate?.Invoke(name, gamePort, queryPort, maxPlayers);
        }

        public void UpdateMaps(IList<string> maps)
        {
            _maps.Clear();
            for (var i = 0; i < maps.Count; i++)
            {
                _maps.Add(new Map
                {
                    Index = i,
                    Name = maps[i],
                    Size = 0 // TODO: split
                });
            }
            MapsChanged?.Invoke(Maps);
        }

        public void AddPlayer(Player player)
        {
            _players.Add(player);
            PlayerJoin?.Invoke(player);
        }

        public void RemovePlayer(Player player)
        {
            _players.Remove(player);
            PlayerLeft?.Invoke(player);
        }

        public void AddMessage(Message message)
        {
            _chatBuffer.Add(message);
            ChatMessage?.Invoke(message);
        }

        private class CircularBuffer<T>
        {
            private readonly T[] _buffer;
            private int _nextFree;

            public CircularBuffer(int length)
            {
                _buffer = new T[length];
                _nextFree = 0;
            }

            public void Add(T item)
            {
                _buffer[_nextFree] = item;
                _nextFree = (_nextFree + 1) % _buffer.Length;
            }

            public IEnumerable<T> Get()
            {
                return _buffer.ToList();
            }
        }
    }
}
