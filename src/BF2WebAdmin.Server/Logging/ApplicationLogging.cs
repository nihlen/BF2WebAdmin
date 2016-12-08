using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace BF2WebAdmin.Server.Logging
{
    public static class ApplicationLogging
    {
        private static ILoggerFactory LoggerFactory { get; }
        public static Microsoft.Extensions.Logging.ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

        static ApplicationLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.LiterateConsole()
                .WriteTo.RollingFile(@"Logs/server-{Date}.log", LogEventLevel.Verbose, "{Timestamp:HH:mm:ss} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}", null, 5000000, 12)
                .CreateLogger();

            LoggerFactory = new LoggerFactory()
                //.AddConsole()
                //.AddDebug()
                .AddSerilog();
        }
    }
}