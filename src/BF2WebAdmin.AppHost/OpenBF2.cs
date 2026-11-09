using Microsoft.Extensions.Configuration;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BF2WebAdmin.AppHost;

/// <summary>
/// An optional OpenBF2 game server with the server extension (libserverext.so), as a container that starts only from the
/// dashboard. Nothing is pulled or created until then. The AppHost sets up both ends of the admin channel: the
/// container's admin channel settings (ENV_ADMIN_CONNECT, ENV_ADMIN_SERVER_ID, ENV_ADMIN_KEY, ENV_ADMIN_CERT_SHA256, as
/// for the production images of bf2-docker) and the matching ServerSettings:GameServers entry for the server, so the game
/// server shows up in WebAdmin without adding it. The server has no rcon: console lines go through WebAdmin.
/// </summary>
public static class OpenBF2
{
    // Must not be 16567 (reserved for the author's sessions) or the localtest's ports (openbf2-server-ext docs/system.md)
    private const int DefaultGamePort = 16575;
    private const int DefaultQueryPort = 29907;
    private const int DefaultBotPort = 16708;

    // The localtest's image (BF2WebAdmin-localtest openbf2/build.sh: the production 2v2 image with local builds of
    // OpenBF2 and the module, its bot channel on 16705 of all interfaces); it exists only on machines that built it
    private const string DefaultImage = "bf2wa-localtest-openbf2:latest";

    // An index of its own, so it does not merge with servers configured in the server's appsecrets.json
    private const int GameServersIndex = 50;

    public static void AddOpenBF2Server(this IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> server, int adminChannelPort)
    {
        var config = builder.Configuration.GetSection("OpenBF2");
        var gamePort = config.GetValue("GamePort", DefaultGamePort);
        var queryPort = config.GetValue("QueryPort", DefaultQueryPort);
        var botPort = config.GetValue("BotPort", DefaultBotPort);
        var (image, tag) = SplitImage(config["Image"] ?? DefaultImage);

        // New for every AppHost run unless configured: the container is recreated on every run as well. The key is the
        // admin channel's HMAC key (hex), the same on both ends
        var adminKey = config["AdminKey"] ?? Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        // The game server pins WebAdmin's certificate, so the AppHost uses (or creates) the one the server uses and tells
        // the server where it is
        var certificatePath = config["CertificatePath"] ?? DefaultCertificatePath;
        var pin = LoadOrCreateCertificatePin(certificatePath);
        server.WithEnvironment("ServerSettings__AdminChannel__CertificatePath", certificatePath);

        var serverId = $"127.0.0.1:{gamePort}";
        var prefix = $"ServerSettings__GameServers__{GameServersIndex}__";
        server
            .WithEnvironment(prefix + "ServerId", serverId)
            .WithEnvironment(prefix + "IpAddress", "127.0.0.1")
            .WithEnvironment(prefix + "GamePort", gamePort.ToString())
            .WithEnvironment(prefix + "QueryPort", queryPort.ToString())
            .WithEnvironment(prefix + "AdminKey", adminKey)
            // Bots and players without a CD-key hash are keyed by name
            .WithEnvironment(prefix + "IsTestServer", "true")
            .WithEnvironment(prefix + "ServerGroup", config["ServerGroup"] ?? "default")
            .WithEnvironment(prefix + "DiscordBot__Token", "")
            .WithEnvironment(prefix + "DiscordBot__AdminChannel", "")
            .WithEnvironment(prefix + "DiscordBot__NotificationChannel", "")
            .WithEnvironment(prefix + "DiscordBot__MatchResultChannel", "");

        var adminConnect = $"host.docker.internal:{adminChannelPort}";
        builder.AddContainer("openbf2", image, tag)
            // A local image: never look for it in a registry
            .WithImagePullPolicy(ImagePullPolicy.Never)
            .WithExplicitStart()
            .WithParentRelationship(server)
            .WaitFor(server)
            .WithContainerRuntimeArgs("--add-host", "host.docker.internal:host-gateway")
            .WithEnvironment("ENV_SERVER_NAME", config["ServerName"] ?? "BF2WebAdmin dev (OpenBF2)")
            .WithEnvironment("ENV_MAX_PLAYERS", "16")
            .WithEnvironment("ENV_SERVER_PORT", gamePort.ToString())
            .WithEnvironment("ENV_GAMESPY_PORT", queryPort.ToString())
            // Not announced to BF2Hub (the localtest image's default too)
            .WithEnvironment("ENV_INTERNET", "0")
            .WithEnvironment("ENV_ADMIN_CONNECT", adminConnect)
            .WithEnvironment("ENV_ADMIN_SERVER_ID", serverId)
            .WithEnvironment("ENV_ADMIN_KEY", adminKey)
            .WithEnvironment("ENV_ADMIN_CERT_SHA256", pin)
            // Appended to serverext.cfg by the localtest image: for the bot channel's test client
            .WithEnvironment("SERVEREXT_CFG_EXTRA", "allowTeleport 1;allowRoundRestart 1")
            // BF2 announces its own ports, so the host ports are the container ports (no proxy). The bot channel listens
            // on a fixed port inside the image
            .WithPortEndpoint("game", gamePort, gamePort, ProtocolType.Udp)
            .WithPortEndpoint("query", queryPort, queryPort, ProtocolType.Udp)
            .WithPortEndpoint("bots", botPort, 16705, ProtocolType.Tcp);
    }

    private static IResourceBuilder<ContainerResource> WithPortEndpoint(this IResourceBuilder<ContainerResource> container, string name, int port,
        int targetPort, ProtocolType protocol) =>
        container.WithEndpoint(name, endpoint =>
        {
            endpoint.Port = port;
            endpoint.TargetPort = targetPort;
            endpoint.Protocol = protocol;
            endpoint.UriScheme = protocol == ProtocolType.Udp ? "udp" : "tcp";
            endpoint.IsProxied = false;
        });

    private static (string Image, string Tag) SplitImage(string reference)
    {
        // The tag follows the last colon after the last slash (a registry host can have a port)
        var colon = reference.LastIndexOf(':');
        return colon > reference.LastIndexOf('/') ? (reference[..colon], reference[(colon + 1)..]) : (reference, "latest");
    }

    // The same path and certificate as BF2WebAdmin.Server's AdminCertificate (its default, without a password)
    private static string DefaultCertificatePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BF2WebAdmin", "admin-channel.pfx");

    /// <summary>serverext.cfg's adminCertSha256 for the certificate at path, creating it as the server would if it is missing.</summary>
    private static string LoadOrCreateCertificatePin(string path)
    {
        if (!File.Exists(path))
        {
            // As AdminCertificate.LoadOrCreate in BF2WebAdmin.Server
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=BF2WebAdmin admin channel", key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            var now = DateTimeOffset.UtcNow.AddDays(-1);
            using var created = request.CreateSelfSigned(now, now.AddYears(100));
            File.WriteAllBytes(path, created.Export(X509ContentType.Pfx, (string?)null));
        }

        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, null, X509KeyStorageFlags.EphemeralKeySet);
        return Convert.ToHexStringLower(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo()));
    }
}
