//using Microsoft.Extensions.Logging;
//using Serilog;
//using Serilog.Core;
//using Serilog.Events;

//namespace BF2WebAdmin.Server.Logging
//{
//    public static class ApplicationLogging
//    {
//        private static ILoggerFactory LoggerFactory { get; }
//        public static Microsoft.Extensions.Logging.ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

//        static ApplicationLogging()
//        {
//            var levelSwitch = new LoggingLevelSwitch();

//            // TODO: appsecrets
//            Log.Logger = new LoggerConfiguration()
//                .MinimumLevel.Verbose()
//                .Enrich.FromLogContext()
//                .WriteTo.Console()
//                .WriteTo.File(@"Logs/server-.log", LogEventLevel.Verbose, "{Timestamp:HH:mm:ss} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31)
//                .CreateLogger();

//            LoggerFactory = new LoggerFactory()
//                //.AddConsole()
//                //.AddDebug()
//                .AddSerilog();
//        }
//    }
//}
