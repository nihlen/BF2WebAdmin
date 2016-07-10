using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace BF2WebAdmin.Server.Logging
{
    public static class ApplicationLogging
    {
        private static ILoggerFactory LoggerFactory { get; }
        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

        static ApplicationLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.LiterateConsole(LogEventLevel.Debug)
                .WriteTo.RollingFile(@"Logs/server-{Date}.log", LogEventLevel.Information, "{Timestamp:HH:mm:ss} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}", null, 5000000, 12)
                .CreateLogger();

            LoggerFactory = new LoggerFactory()
                //.AddConsole()
                //.AddDebug()
                .AddSerilog();
        }
    }
}