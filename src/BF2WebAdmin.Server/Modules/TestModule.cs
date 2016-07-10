using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Logging;
using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server.Modules
{
    public class TestModule : IModule
    {
        private static ILogger Logger { get; } = ApplicationLogging.CreateLogger<TestModule>();
    }
}