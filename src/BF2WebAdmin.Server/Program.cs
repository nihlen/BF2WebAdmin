using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var configPath = Path.Combine(ProjectContext.GetDirectory(), "Configuration");
    var profile = ProjectContext.GetEnvironmentName();

    builder.Configuration
        .SetBasePath(configPath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsettings.{profile}.json", optional: false, reloadOnChange: false)
        .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsecrets.{profile}.json", optional: false, reloadOnChange: false);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var seqSettings = services.GetService<IOptions<SeqSettings>>()?.Value;
        ArgumentNullException.ThrowIfNull(seqSettings);

        configuration
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(@"Logs/server-.log", LogEventLevel.Verbose, "{Timestamp:HH:mm:ss} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31)
            .WriteTo.Seq(seqSettings.ServerUrl, apiKey: seqSettings.ApiKey, controlLevelSwitch: new LoggingLevelSwitch());
    });

    // Add services to the container.

    builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));
    builder.Services.Configure<SeqSettings>(builder.Configuration.GetSection("Seq"));
    builder.Services.AddSingleton<ISocketServer>(services =>
    {
        var settings = services.GetService<IOptions<ServerSettings>>()?.Value;
        ArgumentNullException.ThrowIfNull(settings);

        // TODO: resolve ip somewhere else
        var serverIp = settings.IpAddress.GetIpAddressAsync().GetAwaiter().GetResult();
        return new SocketServer(serverIp, settings.Port, settings.GameServers, settings.PrintSendLog, settings.PrintRecvLog);
    });
    builder.Services.AddHostedService<BF2WebAdminService>();
    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    Log.Information("BF2WebAdmin.Server is starting");
    Log.Information("Current profile: {profile}", profile);

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
    }

    app.UseStaticFiles();
    app.UseRouting();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller}/{action=Index}/{id?}"
    );

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
}
finally
{
    Log.CloseAndFlush();
}
