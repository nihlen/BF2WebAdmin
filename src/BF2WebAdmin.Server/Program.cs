using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Controllers;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Server.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(@"Logs/server-.log", LogEventLevel.Verbose, "{Timestamp:HH:mm:ss} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31)
            .WriteTo.Seq(seqSettings.ServerUrl, apiKey: seqSettings.ApiKey, controlLevelSwitch: new LoggingLevelSwitch());
    });

    // Add services to the container.

    var mqSettings = builder.Configuration.GetSection("RabbitMQ").Get<RabbitMqSettings>();
    ArgumentNullException.ThrowIfNull(mqSettings);

    builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));
    builder.Services.Configure<SeqSettings>(builder.Configuration.GetSection("Seq"));
    builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Authentication"));
    builder.Services.AddMassTransit(x =>
    {
        //x.AddConsumersFromNamespaceContaining<GameStreamConsumer>();

        x.SetKebabCaseEndpointNameFormatter();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(mqSettings.Host, mqSettings.Port, mqSettings.VirtualHost, host =>
            {
                host.Username(mqSettings.Username);
                host.Password(mqSettings.Password);
            });

            //cfg.ConfigureEndpoints(context);

            cfg.ReceiveEndpoint("game-stream-server", e =>
            {
                //e.Consumer<GameStreamConsumer>();
                e.Consumer(() => context.GetRequiredService<GameStreamConsumer>());
            });
        });
    });
    builder.Services.AddMassTransitHostedService(true);
    builder.Services.AddSingleton<GameStreamConsumer>();
    builder.Services.AddSingleton<ISocketServer>(services =>
    {
        var settings = services.GetService<IOptions<ServerSettings>>()?.Value;
        ArgumentNullException.ThrowIfNull(settings);

        // TODO: resolve ip somewhere else
        var serverIp = settings.IpAddress.GetIpAddressAsync().GetAwaiter().GetResult();
        return new SocketServer(serverIp, settings.Port, settings.GameServers, services, settings.PrintSendLog, settings.PrintRecvLog);
    });
    builder.Services.AddHostedService<BF2WebAdminService>();
    builder.Services.AddSignalR()
        //.AddMessagePackProtocol();
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
        });
    builder.Services.AddControllersWithViews();
    // TODO: DataProtection to persist logins
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.AccessDeniedPath = "/Forbidden/";
        });
    builder.Services.AddRazorPages();
    builder.Services.AddResponseCompression(opts =>
    {
        opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" });
    });
    builder.Services.AddOpenTelemetryMetrics(b => b
        //.AddConsoleExporter()
        //.AddConsoleExporter(o =>
        //{
        //    o.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug;
        //})
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://localhost:4317"); // TODO: config
            o.Protocol = OtlpExportProtocol.Grpc;
        })
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BF2WA"))
        //.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "BF2WA", serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
    );
    builder.Services.AddOpenTelemetryTracing(b => b
        //.AddConsoleExporter()
        //.AddConsoleExporter(o =>
        //{
        //    o.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug;
        //})
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://localhost:4317"); // TODO: config
            o.Protocol = OtlpExportProtocol.Grpc;
        })
        //.AddSource("BF2WA")
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BF2WA"))
        //.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: "BF2WA", serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddSqlClientInstrumentation()
        //.AddMassTransitInstrumentation()
        //.AddRedisInstrumentation() // this somehow breaks exporting - nothing is logged
    );

    var app = builder.Build();

    Log.Information("BF2WebAdmin.Server is starting");
    Log.Information("Current profile: {profile}", profile);

    // Set ServerHub static for use in WebModule
    //ServerHub.Current = app.Services.GetRequiredService<IHubContext<ServerHub, IServerHubClient>>();

    app.UseResponseCompression();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseWebAssemblyDebugging();
    }
    else
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapRazorPages();
    app.MapControllers();
    app.MapHub<ServerHub>("/hubs/server");
    app.MapFallbackToFile("index.html");

    //var myActivitySource = new ActivitySource("BF2WA");
    //using (var activity = myActivitySource.StartActivity("SayHello"))
    //{
    //    activity?.SetTag("foo", 1);
    //    Log.Information("activity done");
    //}

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
