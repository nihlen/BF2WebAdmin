using System.Security.Claims;
using System.Text.Json.Serialization;
using AspNetCore.Authentication.ApiKey;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Data;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Data.Repositories;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Api;
using BF2WebAdmin.Server.Controllers;
using BF2WebAdmin.Server.Extensions;
using BF2WebAdmin.Server.Hubs;
using BF2WebAdmin.Server.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

// Log.Logger = new LoggerConfiguration()
//     .WriteTo.Console()
//     .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var configPath = Path.Combine(ProjectContext.GetDirectory(), "Configuration");
    var profile = ProjectContext.GetEnvironmentName();

    // The certificate commands print and exit; they also work without appsecrets.json (docker run --rm <image> ...)
    var certificateCommand = args.Contains("--admin-pin") || args.Contains("--admin-cert-create");

    // The one-off database copy SQL Server -> Postgres (docs/database.md); the target can come from the environment
    var copyCommand = args.Contains(DatabaseCopyCommand.Flag);

    builder.Configuration
        .SetBasePath(configPath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsettings.{profile}.json", optional: true, reloadOnChange: false)
        .AddJsonFile("appsecrets.json", optional: certificateCommand || copyCommand, reloadOnChange: false)
        .AddJsonFile($"appsecrets.{profile}.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables();

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

    if (certificateCommand)
    {
        var adminChannel = builder.Configuration.GetSection("ServerSettings:AdminChannel").Get<AdminChannelSettings>() ?? new AdminChannelSettings();
        if (args.Contains("--admin-cert-create"))
        {
            // A new certificate for ServerSettings:AdminChannel:Certificate (a secret). Nothing is written to disk
            var pem = args.Contains("--pem");
            using var created = AdminCertificate.Create();
            var encrypted = !string.IsNullOrEmpty(adminChannel.CertificatePassword);
            Console.WriteLine($"# {AdminCertificate.CertificateKey} (env ServerSettings__AdminChannel__Certificate): " +
                (pem ? "PEM, certificate and private key" : "base64 PKCS#12") +
                (encrypted ? ", encrypted with the configured CertificatePassword" : ", no password"));
            Console.WriteLine(AdminCertificate.Export(created, pem, adminChannel.CertificatePassword));
            Console.WriteLine("# serverext.cfg on every game server");
            Console.WriteLine($"adminCertSha256 {AdminCertificate.Pin(created)}");
        }
        else
        {
            // Prints the adminCertSha256 value for serverext.cfg of the configured certificate (the file is created if
            // there is neither a Certificate value nor a file)
            using var certificate = AdminCertificate.Load(adminChannel, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out _);
            Console.WriteLine(AdminCertificate.Pin(certificate));
        }

        return;
    }

    if (copyCommand)
    {
        Environment.ExitCode = await DatabaseCopyCommand.RunAsync(args, builder.Configuration);
        return;
    }

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

    builder.Services.AddSingleton<ViewerRegistry>();
    builder.Services.AddSingleton<BF2WebAdmin.Server.Api.CommandLog>();
    builder.Services.AddSingleton(services => new GameServerRegistry(serverSettings.GameServers, services));
    builder.Services.AddSingleton<IGameServerRegistry>(services => services.GetRequiredService<GameServerRegistry>());
    builder.Services.AddSingleton(services => new AdminListener(serverSettings.AdminChannel, services.GetRequiredService<GameServerRegistry>(),
        services.GetRequiredService<ILoggerFactory>()));

    // Player joins for the BF2v2 site's dbo.JoinHistory, written in the background
    builder.Services.AddSingleton<JoinHistoryWriter>();
    builder.Services.AddSingleton<IJoinHistoryWriter>(services => services.GetRequiredService<JoinHistoryWriter>());
    builder.Services.AddHostedService(services => services.GetRequiredService<JoinHistoryWriter>());
    builder.Services.AddHostedService<BF2WebAdminService>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddHostedService<StaleMatchCloser>();
    // The web UI uses JSON with camelCase names and enums as strings, for both the API and the hub
    builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddSignalR()
        .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddControllersWithViews();
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, _, _) =>
    {
        // Since .NET 10 a number is documented as "integer or string", because the web JSON defaults also read numbers
        // from strings. The web UI sends numbers, so its generated types keep plain numbers
        if (schema.Type is { } type && (type & (Microsoft.OpenApi.JsonSchemaType.Integer | Microsoft.OpenApi.JsonSchemaType.Number)) != 0)
        {
            schema.Type = type & ~Microsoft.OpenApi.JsonSchemaType.String;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }));

    // TODO: DataProtection to persist logins
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            // The web UI is a single-page app: answer with status codes instead of redirecting to login pages
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
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

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(ApiEndpoints.AdminPolicy, policy => policy
            .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, ApiKeyDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser());
    });

    builder.Services.AddResponseCompression(opts => { opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" }); });

    // Normal MediatR doesn't work with the singleton SocketServer, but we can setup it up differently 
    // This is not very nice though...
    // var serviceConfig = new MediatRServiceConfiguration();
    // ServiceRegistrar.AddRequiredServices(builder.Services, serviceConfig);
    // builder.Services.AddMediatR(config => config.AsSingleton(), Assembly.GetExecutingAssembly());
    // builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
    // builder.Services.AddSingleton<MediatR.IMediator>(serviceProvider => new MediatR.Mediator(serviceProvider.GetRequiredService));
    // builder.Services.AddSingleton<INotificationHandler<ServerUpdated>>(c => c.GetRequiredService<ISocketServer>());

    // SQLite, SQL Server or Postgres (Database:Provider, else from the connection string; docs/database.md)
    var database = DatabaseSetup.FromConfiguration(builder.Configuration);
    database.AddRepositories(builder.Services, maps: false, joins: true);

    var app = builder.Build();

    app.Logger.LogInformation("BF2WebAdmin.Server is starting");
    app.Logger.LogInformation("Current profile: {Profile}", profile);
    app.Logger.LogInformation("Database: {Provider}", database.Provider);

    // Fails here, before anything listens, when the admin channel certificate setting is unusable
    app.Services.GetRequiredService<AdminListener>().LoadCertificate();

    // EF migrations (SQLite, Postgres) or the idempotent SQL Server schema updates
    await database.MigrateAsync(app.Services, app.Logger);

    app.UseResponseCompression();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler();

        if (serverSettings.ForceHttps)
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
    }

    // The web UI (src/BF2WebAdmin.Web, built into wwwroot on publish). Vite puts a content hash in the names of the files in
    // assets/, so they can be cached for good; index.html is checked on every load. The 3D viewer's models and maps
    // (wwwroot/viewer) are glTF binaries, a type the default provider does not know and would not serve.
    var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    contentTypes.Mappings[".glb"] = "model/gltf-binary";
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = contentTypes,
        OnPrepareResponse = context =>
        {
            context.Context.Response.Headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
                ? "public, max-age=31536000, immutable"
                : "no-cache";
        }
    });

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapApi();
    app.MapHub<ServerHub>("/hubs/server");
    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    // Unknown API paths are 404s, everything else is a route of the web UI
    app.Map("/api/{**path}", () => Results.NotFound());
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
    });

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