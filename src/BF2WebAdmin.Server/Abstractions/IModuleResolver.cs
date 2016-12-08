using System;
using System.Collections.Generic;

namespace BF2WebAdmin.Server.Abstractions
{
    public interface IModuleResolver
    {
        IDictionary<string, IList<Func<string[], ICommand>>> CommandParsers { get; }
        IDictionary<Type, IList<Action<ICommand>>> CommandHandlers { get; }
        IEnumerable<Type> ModuleTypes { get; }
        IDictionary<Type, IModule> Modules { get; }
    }
}