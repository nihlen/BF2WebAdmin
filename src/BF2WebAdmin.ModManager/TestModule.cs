using System;
using System.Collections.Generic;

namespace BF2WebAdmin.ModManager
{
    public class TestModule : IModule,
        IHandleCommand<RankCommand>
    {
        public void Start()
        {
            
        }

        public void Stop()
        {
            
        }

        public void Handle(RankCommand command)
        {
            throw new NotImplementedException();
        }
    }

    public interface IModule
    {
        void Start();
        void Stop();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CommandAttribute : Attribute
    {
        public string[] Aliases { get; }

        public CommandAttribute(string format)
        {
            var parts = format.Split(' ');
        }

        private void SplitAliases(string aliasRaw)
        {
            
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class CommandParameterAttribute : Attribute
    {
        public CommandParameterAttribute(params string[] aliases)
        {
            Aliases = aliases;
        }
    }

    public interface IHandleCommand<in TCommand> where TCommand : ICommand
    {
        void Handle(TCommand command);
    }

    public interface ICommand
    {
        ICommand Parse(string message);
    }

    [Command("r|rank <Name> <Rank>")]
    public class RankCommand : ICommand
    {
        public string Name { get; set; }
        public int Rank { get; set; }
    }

}