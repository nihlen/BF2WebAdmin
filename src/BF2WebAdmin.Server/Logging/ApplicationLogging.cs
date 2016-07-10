using Microsoft.Extensions.Logging;

namespace BF2WebAdmin.Server.Logging
{
    public static class ApplicationLogging
    {
        public static ILoggerFactory LoggerFactory { get; }
        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

        static ApplicationLogging()
        {
            LoggerFactory = new LoggerFactory()
                .AddConsole()
                .AddDebug();
        }
    }
}