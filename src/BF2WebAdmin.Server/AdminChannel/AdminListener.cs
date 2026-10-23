using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace BF2WebAdmin.Server.AdminChannel;

public class AdminChannelSettings
{
    /// <summary>TLS listener for the modules' adminConnect. Never 16567.</summary>
    public int Port { get; set; } = 4310;

    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>PKCS#12 file with the certificate and key; created on first start. Default: %LOCALAPPDATA%/BF2WebAdmin/admin-channel.pfx.</summary>
    public string? CertificatePath { get; set; }

    public string? CertificatePassword { get; set; }

    /// <summary>Optional plain-TCP listener on 127.0.0.1 for a module with adminTls 0 (loopback tests only).</summary>
    public int? PlainLoopbackPort { get; set; }
}

/// <summary>
/// Accepts the modules' admin channel connections: TLS 1.3 with WebAdmin's pinned certificate, then the
/// HMAC handshake (both within 10 s of the TCP connect), then an <see cref="AdminSession"/> on the server's
/// <see cref="Entities.GameServer"/>.
/// </summary>
public class AdminListener
{
    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly AdminChannelSettings _settings;
    private readonly GameServerRegistry _registry;
    private readonly ILogger<AdminListener> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private X509Certificate2? _certificate;

    public AdminListener(AdminChannelSettings settings, GameServerRegistry registry, ILoggerFactory loggerFactory)
    {
        _settings = settings;
        _registry = registry;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AdminListener>();
    }

    public string? CertificatePin { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_settings.Port == 16567 || _settings.PlainLoopbackPort == 16567)
            throw new InvalidOperationException("The admin channel must not use port 16567");

        _certificate = AdminCertificate.LoadOrCreate(_settings.CertificatePath, _settings.CertificatePassword, _logger);
        CertificatePin = AdminCertificate.Pin(_certificate);
        _logger.LogInformation("Admin channel: TLS certificate {Subject}, pin for serverext.cfg: adminCertSha256 {Pin}", _certificate.Subject, CertificatePin);

        var listeners = new List<Task> { ListenAsync(new IPEndPoint(IPAddress.Parse(_settings.BindAddress), _settings.Port), true, cancellationToken) };
        if (_settings.PlainLoopbackPort is { } plainPort)
            listeners.Add(ListenAsync(new IPEndPoint(IPAddress.Loopback, plainPort), false, cancellationToken));

        await Task.WhenAll(listeners);
    }

    private async Task ListenAsync(IPEndPoint endPoint, bool tls, CancellationToken cancellationToken)
    {
        var listener = new TcpListener(endPoint);
        listener.Start();
        _logger.LogInformation("Admin channel: listening on {EndPoint} ({Mode})", endPoint, tls ? "TLS 1.3" : "plain, loopback only");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                AppDiagnostics.ConnectedClientsCounter.Add(1);
                _ = Task.Run(() => HandleConnectionAsync(client, tls, cancellationToken), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            listener.Stop();
            _logger.LogInformation("Admin channel: stopped listening on {EndPoint}", endPoint);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, bool tls, CancellationToken cancellationToken)
    {
        var remote = (IPEndPoint)client.Client.RemoteEndPoint!;
        client.NoDelay = true;
        Stream stream = client.GetStream();
        AdminSession? session = null;

        try
        {
            AdminHandshakeResult result;
            var decoder = new AdminFrameDecoder();
            using (var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                handshakeCts.CancelAfter(HandshakeTimeout);
                if (tls)
                {
                    var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                    stream = ssl;
                    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _certificate,
                        EnabledSslProtocols = SslProtocols.Tls13,
                        ClientCertificateRequired = false,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    }, handshakeCts.Token);
                    _logger.LogDebug("Admin channel: TLS up with {Remote} ({Protocol}, {Cipher})", remote, ssl.SslProtocol, ssl.NegotiatedCipherSuite);
                }

                result = await AdminHandshake.RunAsync(stream, decoder, _registry.LookupKeyAsync, handshakeCts.Token);
            }

            var server = await _registry.GetOrCreateServerAsync(result.ServerId);
            session = new AdminSession(result.ServerId, remote, stream, decoder, server.Applier, _loggerFactory.CreateLogger<AdminSession>(), cancellationToken);
            var previous = server.Session;
            server.AttachSession(session);
            previous?.Close("replaced by a new link from the server");
            AppDiagnostics.AcceptedClientsCounter.Add(1);
            _logger.LogInformation("Admin channel: {ServerId} authenticated from {Remote} ({Build})", result.ServerId, remote, result.Hello.Build);

            await session.RunAsync();
        }
        catch (Exception ex) when (session == null)
        {
            AppDiagnostics.RejectedClientsCounter.Add(1);
            var reason = ex switch
            {
                OperationCanceledException when !cancellationToken.IsCancellationRequested => $"no handshake within {HandshakeTimeout.TotalSeconds:0} s",
                OperationCanceledException => "shutting down",
                AuthenticationException => $"TLS failed (a module whose adminCertSha256 differs aborts here): {ex.Message}",
                AdminHandshakeException or AdminProtocolException => ex.Message,
                IOException => $"connection lost: {ex.Message}",
                _ => ex.ToString(),
            };
            _logger.LogWarning("Admin channel: refused {Remote}: {Reason}", remote, reason);
            await stream.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin channel: session from {Remote} failed", remote);
        }
        finally
        {
            client.Dispose();
        }
    }
}
