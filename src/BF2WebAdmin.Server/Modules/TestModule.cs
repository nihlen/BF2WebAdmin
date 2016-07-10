using System.Reflection;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Logging;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server.Modules
{
    public class TestModule : IModule
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<TestModule>();
    }
}