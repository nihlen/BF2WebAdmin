using System;
using BF2WebAdmin.Server.Entities;

namespace BF2WebAdmin.ModManager.Modules
{
    public class BF2Module : IModule
    {
        private IGameServer _gameServer;

        public BF2Module(IGameServer server)
        {
            _gameServer = server;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Handle(RankCommand command)
        {
            
        }
    }

    public interface IModule : IDisposable
    {
        void Start();
        void Stop();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CommandAttribute : Attribute
    {
        public string[] Aliases { get; }

        public CommandAttribute(params string[] aliases)
        {
            Aliases = aliases;
            
        }
    }

    public interface IHandleCommand<in TCommand> where TCommand : ICommand
    {
        string Handle(TCommand command);
    }

    public interface ICommand
    {
    }
}