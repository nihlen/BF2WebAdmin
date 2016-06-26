using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BF2WebAdmin.Server.Entities;
using log4net;

namespace BF2WebAdmin.ModManager
{
    public interface IModManager
    {
        //event Action<string, int, int, int> ServerUpdate;
        //event Action<Message> ChatMessage;
    }

    public class ModManager : IModManager
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //public event Action<string, int, int, int> ServerUpdate;
        //public event Action<Message> ChatMessage;

        private IGameServer _gameServer;
        private static readonly string CommandPrefix = ".";
        private Dictionary<string, ICommand> _commandHandlers = new Dictionary<string, ICommand>();

        static ModManager()
        {
            ScanAssembly();
        }

        public ModManager(IGameServer gameServer)
        {
            _gameServer = gameServer;
            gameServer.ChatMessage += HandleChatMessage;
        }

        private void HandleChatMessage(Message message)
        {
            if (message.Type != MessageType.Player)
                return;
            if (!message.Text.StartsWith(CommandPrefix))
                return;

            Log.Info("Found command: ");
            throw new NotImplementedException();
        }

        private static void ScanAssembly()
        {
            //var handlers = Assembly.GetCallingAssembly()
            //    .GetTypes()
            //    .Where(type => type.GetInterfaces().Any(i => i.IsGenericType
            //        && i.GetGenericTypeDefinition() == typeof(IHandleCommand<>)))
            //    .Select(t => new
            //    {

            //    });

            var ass = Assembly.GetCallingAssembly();

            var wtf = (
                from t in ass.GetTypes()
                let interfaces = t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleCommand<>))
                from i in interfaces
                let args = i.GetGenericArguments()
                select new What
                {
                    CommandType = args[0],
                    AggregateType = t
                }).ToList();

            Console.WriteLine(wtf.Count);

            //var ass = Assembly.GetCallingAssembly();

            //var types = ass.GetTypes();

            //foreach (var type in types)
            //{
            //    var interfaces = type.GetInterfaces()
            //        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleCommand<>));

            //    foreach (var @interface in interfaces)
            //    {
            //        var args = @interface.GetGenericArguments();
            //        wtf.Add(new What { CommandType = args[0], AggregateType = type });

            //    }
            //}

            //var handlers =
            //    from t in Assembly.GetCallingAssembly().GetTypes()
            //    from i in t.GetInterfaces()
            //    where i.IsGenericType
            //    where i.GetGenericTypeDefinition() == typeof(IHandleCommand<>)
            //    let args = i.GetGenericArguments()
            //    select new
            //    {
            //        CommandType = args[0],
            //        AggregateType = t
            //    };
        }

        public class What
        {
            public Type CommandType { get; set; }
            public Type AggregateType { get; set; }
        }
    }
}
