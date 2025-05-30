﻿using System.Security.Claims;
using AspNetCore.Authentication.ApiKey;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Repositories;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Controllers;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Server.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Nihlen.Common.Telemetry;

// Log.Logger = new LoggerConfiguration()
//     .WriteTo.Console()
//     .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var configPath = Path.Combine(ProjectContext.GetDirectory(), "Configuration");
    var profile = ProjectContext.GetEnvironmentName();

    builder.Configuration
        .SetBasePath(configPath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsettings.{profile}.json", optional: true, reloadOnChange: false)
        .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsecrets.{profile}.json", optional: true, reloadOnChange: false);

    builder.Services.AddCustomTelemetry("bf2-webadmin", otlpEndpoint: builder.Configuration["Telemetry:OtlpEndpoint"]);

    // builder.Host.UseSerilog((context, services, configuration) =>
    // {
    //     configuration
    //         .MinimumLevel.Debug()
    //         .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    //         .Enrich.FromLogContext()
    //         .WriteTo.Console()
    //         .WriteTo.File(@"Logs/server-.log", LogEventLevel.Verbose, "{Timestamp:HH:mm:ss} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);
    //
    //     var seqSettings = services.GetService<IOptions<SeqSettings>>()?.Value;
    //     if (!string.IsNullOrWhiteSpace(seqSettings?.ServerUrl))
    //     {
    //         configuration.WriteTo.Seq(seqSettings.ServerUrl, apiKey: seqSettings.ApiKey, controlLevelSwitch: new LoggingLevelSwitch());
    //     }
    // });

    // Add services to the container.

    var authSettings = builder.Configuration.GetSection("Authentication").Get<AuthSettings>();
    ArgumentNullException.ThrowIfNull(authSettings);

    var serverSettings = builder.Configuration.GetSection("ServerSettings").Get<ServerSettings>();
    ArgumentNullException.ThrowIfNull(serverSettings);

    builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Authentication"));
    builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection("ServerSettings"));

    var mqSettings = builder.Configuration.GetSection("RabbitMQ").Get<RabbitMqSettings>();
    if (!string.IsNullOrWhiteSpace(mqSettings?.Host))
    {
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
    }

    builder.Services.AddSingleton<ISocketServer>(services =>
    {
        // TODO: resolve ip somewhere else
        var serverIp = serverSettings.IpAddress.GetIpAddressAsync().GetAwaiter().GetResult();
        return new SocketServer(serverIp, serverSettings.Port, serverSettings.GameServers, services, serverSettings.PrintSendLog, serverSettings.PrintRecvLog);
    });

    builder.Services.AddHostedService<BF2WebAdminService>();
    builder.Services.AddSignalR()
        .AddMessagePackProtocol();

    // .AddJsonProtocol(options =>
    // {
    //     options.PayloadSerializerOptions.PropertyNamingPolicy = null;
    // });

    builder.Services.AddControllersWithViews();

    // TODO: DataProtection to persist logins
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.AccessDeniedPath = "/Forbidden/";
        })
        .AddApiKeyInHeader(ApiKeyDefaults.AuthenticationScheme, options =>
        {
            options.Realm = "bf2-webadmin";
            options.KeyName = "X-API-KEY";
            options.Events = new ApiKeyEvents
            {
                OnValidateKey = async (context) =>
                {
                    var apiKey = authSettings.ApiKey;
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        context.NoResult();
                        return;
                    }

                    var isValid = apiKey.Equals(context.ApiKey, StringComparison.OrdinalIgnoreCase);
                    if (isValid)
                    {
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "api-user", ClaimValueTypes.String, context.Options.ClaimsIssuer),
                            new Claim(ClaimTypes.Name, "api-user", ClaimValueTypes.String, context.Options.ClaimsIssuer),
                        };
                        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                        context.Success();
                    }
                    else
                    {
                        context.NoResult();
                    }
                },
            };
        });

    builder.Services.AddRazorPages();
    builder.Services.AddResponseCompression(opts => { opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" }); });

    // Normal MediatR doesn't work with the singleton SocketServer, but we can setup it up differently 
    // This is not very nice though...
    // var serviceConfig = new MediatRServiceConfiguration();
    // ServiceRegistrar.AddRequiredServices(builder.Services, serviceConfig);
    // builder.Services.AddMediatR(config => config.AsSingleton(), Assembly.GetExecutingAssembly());
    // builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
    // builder.Services.AddSingleton<MediatR.IMediator>(serviceProvider => new MediatR.Mediator(serviceProvider.GetRequiredService));
    // builder.Services.AddSingleton<INotificationHandler<ServerUpdated>>(c => c.GetRequiredService<ISocketServer>());

    var connectionString = builder.Configuration.GetConnectionString("BF2DB");
    if (connectionString.Contains(".sqlite"))
    {
        // SQLite
        builder.Services.AddDbContext<BF2Context>(o => o.UseSqlite(connectionString));
        builder.Services.AddScoped<IServerSettingsRepository, ServerSettingsRepository>();
    }
    else
    {
        builder.Services.AddSingleton<IServerSettingsRepository>(c => new SqlServerSettingsRepository(connectionString));
    }

    var app = builder.Build();

    app.Logger.LogInformation("BF2WebAdmin.Server is starting");
    app.Logger.LogInformation("Current profile: {Profile}", profile);

    if (connectionString.Contains(".sqlite"))
    {
        // SQLite initialization
        app.Logger.LogInformation("Running SQLite migrations");
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BF2Context>();
        await context.Database.MigrateAsync();
    }

    app.UseResponseCompression();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseWebAssemblyDebugging();
    }
    else
    {
        app.UseExceptionHandler("/Error");

        if (serverSettings.ForceHttps)
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
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

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("An unhandled exception occurred during bootstrapping: " + ex);
    throw;

    // Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
}

// finally
// {
//     Log.CloseAndFlush();
// }