using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Server.AdminChannel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>Handshake, TLS pinning and session behaviour against a fake module over loopback TCP.</summary>
public class AdminChannelTests
{
    private const string ServerId = "127.0.0.1:16569";
    private static readonly ConcurrentBag<TcpClient> KeepAlive = new();
    private static readonly byte[] Key = Convert.FromHexString("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff");

    private static async Task<(Stream WebAdmin, Stream Module, IPEndPoint ModuleEndPoint)> ConnectAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new TcpClient();
        var connect = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var accepted = await listener.AcceptTcpClientAsync();
        await connect;
        listener.Stop();

        // TcpClient's finalizer closes its socket, so keep both alive for the test run
        KeepAlive.Add(client);
        KeepAlive.Add(accepted);
        return (accepted.GetStream(), client.GetStream(), (IPEndPoint)client.Client.LocalEndPoint!);
    }

    /// <summary>The module's side, as libserverext does it.</summary>
    private sealed class FakeModule(Stream stream, byte[] key, string serverId = ServerId, ushort version = 1)
    {
        public readonly byte[] Nonce = RandomNumberGenerator.GetBytes(32);
        public Stream Stream => stream;
        private readonly AdminFrameDecoder _decoder = new();

        public Task SendAsync(AdminMessage m) => stream.WriteAsync(AdminProtocol.Encode(m)).AsTask();

        public async Task<AdminMessage> ReadAsync(TimeSpan? timeout = null)
        {
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
            var buffer = new byte[65536];
            while (true)
            {
                if (_decoder.TryRead(out var type, out var payload))
                    return AdminProtocol.Decode(type, payload);
                var n = await stream.ReadAsync(buffer, cts.Token);
                if (n == 0)
                    throw new EndOfStreamException();
                _decoder.Append(buffer.AsSpan(0, n));
            }
        }

        /// <summary>Hello, then checks WebAdmin's proof; on success Auth, Welcome and Snapshot without waiting.</summary>
        public async Task<bool> HandshakeAsync(byte[]? proofKey = null)
        {
            await SendAsync(new Hello(version, serverId, Nonce, "fake module"));
            if (await ReadAsync() is not Challenge challenge)
                return false;

            var expected = AdminProtocol.Proof(key, AdminProtocol.WebAdminProofLabel, serverId, Nonce, challenge.Nonce);
            if (!CryptographicOperations.FixedTimeEquals(expected, challenge.Proof))
            {
                await SendAsync(new ErrorMessage(0, AdminErrorCode.AuthenticationFailed, "WebAdmin's proof does not match adminKey"));
                return false;
            }

            await SendAsync(new Auth(AdminProtocol.Proof(proofKey ?? key, AdminProtocol.ServerProofLabel, serverId, Nonce, challenge.Nonce)));
            await SendAsync(Welcome());
            await SendAsync(new Snapshot(0, 0, 100, 3.3, Game, [], [], [], []));
            return true;
        }
    }

    private static readonly AdminGameState Game = new(1, 3, -1, 100, 100, 10, 0, 0, 0);

    private static Welcome Welcome() => new(1, 1, 99, 30, "fake", 16569, 29901, 16, 7, "dalian_2_v_2", "gpm_cq", 16, 0,
        [new MapListEntry("dalian_2_v_2", "gpm_cq", 16)], "CH", "US", Game);

    private static ValueTask<byte[]?> Lookup(string id) => ValueTask.FromResult(id == ServerId ? Key : null);

    [Fact]
    public async Task HandshakeWithTheRightKeyAuthenticates()
    {
        var (webAdmin, module, _) = await ConnectAsync();
        var fake = new FakeModule(module, Key);
        var decoder = new AdminFrameDecoder();
        var handshake = AdminHandshake.RunAsync(webAdmin, decoder, Lookup, CancellationToken.None);
        Assert.True(await fake.HandshakeAsync());
        var result = await handshake;
        Assert.Equal(ServerId, result.ServerId);
        Assert.Equal("fake module", result.Hello.Build);
    }

    [Fact]
    public async Task ModuleWithAnotherKeyRefusesWebAdminsProof()
    {
        var (webAdmin, module, _) = await ConnectAsync();
        var other = Key.ToArray();
        other[0] ^= 1;
        var fake = new FakeModule(module, other);
        var handshake = AdminHandshake.RunAsync(webAdmin, new AdminFrameDecoder(), Lookup, CancellationToken.None);
        Assert.False(await fake.HandshakeAsync());
        var e = await Assert.ThrowsAsync<AdminHandshakeException>(() => handshake);
        Assert.Contains("adminKey differs", e.Message);
    }

    [Fact]
    public async Task WrongModuleProofIsRefusedWithError8()
    {
        var (webAdmin, module, _) = await ConnectAsync();
        var fake = new FakeModule(module, Key);
        var handshake = AdminHandshake.RunAsync(webAdmin, new AdminFrameDecoder(), Lookup, CancellationToken.None);
        var wrong = Key.ToArray();
        wrong[^1] ^= 0x80;
        Assert.True(await fake.HandshakeAsync(proofKey: wrong));
        var e = await Assert.ThrowsAsync<AdminHandshakeException>(() => handshake);
        Assert.Equal(AdminErrorCode.AuthenticationFailed, e.Code);
        var error = Assert.IsType<ErrorMessage>(await fake.ReadAsync());
        Assert.Equal(AdminErrorCode.AuthenticationFailed, error.Code);
    }

    [Theory]
    [InlineData("10.0.0.1:16567", 1, AdminErrorCode.AuthenticationFailed)]
    [InlineData(ServerId, 2, AdminErrorCode.VersionMismatch)]
    public async Task UnknownServerOrVersionIsRefused(string serverId, ushort version, AdminErrorCode code)
    {
        var (webAdmin, module, _) = await ConnectAsync();
        var fake = new FakeModule(module, Key, serverId, version);
        var handshake = AdminHandshake.RunAsync(webAdmin, new AdminFrameDecoder(), Lookup, CancellationToken.None);
        Assert.False(await fake.HandshakeAsync());
        Assert.Equal(code, (await Assert.ThrowsAsync<AdminHandshakeException>(() => handshake)).Code);
    }

    [Theory]
    [InlineData("00112233445566778899aabbccddeeff", true)]
    [InlineData(" 00112233445566778899AABBCCDDEEFF00112233445566778899aabbccddeeff ", true)]
    [InlineData("00112233445566778899aabbccddee", false)]
    [InlineData("0011223344556677889zaabbccddeeff", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ParsesKeys(string? hex, bool valid)
    {
        Assert.Equal(valid, AdminHandshake.ParseKey(hex) != null);
    }

    [Fact]
    public async Task TlsWithThePinnedCertificate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bf2wa-admin-test-{Guid.NewGuid():N}.pfx");
        try
        {
            using var certificate = AdminCertificate.LoadOrCreate(path, null, NullLogger.Instance);
            var pin = AdminCertificate.Pin(certificate);
            using var again = AdminCertificate.LoadOrCreate(path, null, NullLogger.Instance);
            Assert.Equal(pin, AdminCertificate.Pin(again));

            var (webAdmin, module, _) = await ConnectAsync();
            var server = new SslStream(webAdmin);
            string? seenPin = null;
            var client = new SslStream(module, false, (_, cert, _, _) =>
            {
                // The module pins the leaf's SPKI and ignores the CA chain
                var leaf = (System.Security.Cryptography.X509Certificates.X509Certificate2)cert!;
                seenPin = Convert.ToHexString(SHA256.HashData(leaf.PublicKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
                return seenPin == pin;
            });

            var serverAuth = server.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls13,
            });
            await client.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "webadmin", EnabledSslProtocols = SslProtocols.Tls13 });
            await serverAuth;
            Assert.Equal(SslProtocols.Tls13, server.SslProtocol);
            Assert.Equal(pin, seenPin);

            var fake = new FakeModule(client, Key);
            var handshake = AdminHandshake.RunAsync(server, new AdminFrameDecoder(), Lookup, CancellationToken.None);
            Assert.True(await fake.HandshakeAsync());
            Assert.Equal(ServerId, (await handshake).ServerId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A TLS 1.3 handshake with WebAdmin's side on <paramref name="certificate"/>; returns the pin the module saw.</summary>
    private static async Task<string> TlsPinAsync(X509Certificate2 certificate)
    {
        var (webAdmin, module, _) = await ConnectAsync();
        using var server = new SslStream(webAdmin);
        string? seenPin = null;
        using var client = new SslStream(module, false, (_, cert, _, _) =>
        {
            seenPin = Convert.ToHexString(SHA256.HashData(((X509Certificate2)cert!).PublicKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
            return true;
        });
        var serverAuth = server.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = certificate,
            EnabledSslProtocols = SslProtocols.Tls13,
        });
        await client.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "webadmin", EnabledSslProtocols = SslProtocols.Tls13 });
        await serverAuth;
        Assert.Equal(SslProtocols.Tls13, server.SslProtocol);

        // Data both ways, so the server's key was really used
        await client.WriteAsync(new byte[] { 1, 2, 3 });
        var buffer = new byte[3];
        await server.ReadExactlyAsync(buffer);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        return seenPin!;
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "secret")]
    [InlineData(true, null)]
    [InlineData(true, "secret")]
    public async Task CertificateFromTheSecretValue(bool pem, string? password)
    {
        // As --admin-cert-create prints it
        using var created = AdminCertificate.Create();
        var value = AdminCertificate.Export(created, pem, password);
        Assert.Equal(pem, value.Contains("-----BEGIN"));
        var settings = new AdminChannelSettings { Certificate = value, CertificatePassword = password };

        using var certificate = AdminCertificate.Load(settings, NullLogger.Instance, out var source);
        Assert.Equal(AdminCertificate.CertificateKey, source);
        Assert.True(certificate.HasPrivateKey);
        Assert.Equal(AdminCertificate.Pin(created), AdminCertificate.Pin(certificate));
        Assert.Equal(AdminCertificate.Pin(created), await TlsPinAsync(certificate));
    }

    [Fact]
    public void CertificateValueAllowsLineBreaks()
    {
        using var created = AdminCertificate.Create();
        var value = AdminCertificate.Export(created, false, null);
        var wrapped = string.Join("\n", value.Chunk(64).Select(c => new string(c))) + "\n";
        using var certificate = AdminCertificate.FromValue(wrapped, null);
        Assert.Equal(AdminCertificate.Pin(created), AdminCertificate.Pin(certificate));
    }

    [Fact]
    public void CertificateValueWinsOverThePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bf2wa-admin-test-{Guid.NewGuid():N}");
        try
        {
            // An existing file at the path is not used
            var existing = Path.Combine(directory, "existing.pfx");
            using (var fromFile = AdminCertificate.LoadOrCreate(existing, null, NullLogger.Instance))
            {
                using var created = AdminCertificate.Create();
                var settings = new AdminChannelSettings { Certificate = AdminCertificate.Export(created, false, null), CertificatePath = existing };
                using var certificate = AdminCertificate.Load(settings, NullLogger.Instance, out _);
                Assert.Equal(AdminCertificate.Pin(created), AdminCertificate.Pin(certificate));
                Assert.NotEqual(AdminCertificate.Pin(fromFile), AdminCertificate.Pin(certificate));
            }

            // A missing file is not created
            var missing = Path.Combine(directory, "missing", "admin-channel.pfx");
            using (var created = AdminCertificate.Create())
            {
                var settings = new AdminChannelSettings { Certificate = AdminCertificate.Export(created, true, null), CertificatePath = missing };
                using var certificate = AdminCertificate.Load(settings, NullLogger.Instance, out _);
                Assert.Equal(AdminCertificate.Pin(created), AdminCertificate.Pin(certificate));
            }

            Assert.False(Directory.Exists(Path.Combine(directory, "missing")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CertificateFromThePathWithoutAValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bf2wa-admin-test-{Guid.NewGuid():N}.pfx");
        try
        {
            using var certificate = AdminCertificate.Load(new AdminChannelSettings { CertificatePath = path, Certificate = " " }, NullLogger.Instance, out var source);
            Assert.Equal(path, source);
            Assert.True(File.Exists(path));
            using var again = AdminCertificate.LoadOrCreate(path, null, NullLogger.Instance);
            Assert.Equal(AdminCertificate.Pin(certificate), AdminCertificate.Pin(again));
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static TheoryData<string> InvalidCertificateValues()
    {
        using var created = AdminCertificate.Create();
        return new TheoryData<string>
        {
            // Not base64
            "not a certificate",
            // Base64, not PKCS#12
            Convert.ToBase64String("hello, this is no certificate"u8.ToArray()),
            // A truncated PKCS#12
            AdminCertificate.Export(created, false, null)[..200],
            // PEM with the certificate only
            created.ExportCertificatePem(),
            // An encrypted PKCS#12 without the password
            AdminCertificate.Export(created, false, "secret"),
        };
    }

    [Theory]
    [MemberData(nameof(InvalidCertificateValues))]
    public void InvalidCertificateValueIsAClearError(string value)
    {
        var settings = new AdminChannelSettings { Certificate = value };
        var ex = Assert.Throws<InvalidOperationException>(() => AdminCertificate.Load(settings, NullLogger.Instance, out _));
        Assert.Contains(AdminCertificate.CertificateKey, ex.Message);
        // The secret never ends up in a log
        Assert.DoesNotContain(value.Trim(), ex.Message);
    }

    [Fact]
    public void ListenerFailsAtStartupOnAnInvalidCertificateValue()
    {
        var listener = new AdminListener(new AdminChannelSettings { Certificate = "bm90IGEgcGZ4" }, null!, NullLoggerFactory.Instance);
        var ex = Assert.Throws<InvalidOperationException>(listener.LoadCertificate);
        Assert.Contains(AdminCertificate.CertificateKey, ex.Message);
        Assert.Null(listener.CertificatePin);
    }

    private sealed class RecordingHandler : IAdminSessionHandler
    {
        public readonly BlockingCollection<AdminMessage> Messages = new();
        public readonly TaskCompletionSource Closed = new();

        /// <summary>When set, the handler waits for it after recording an event with seq 1.</summary>
        public TaskCompletionSource? Gate;

        public async ValueTask OnMessageAsync(AdminSession session, AdminMessage message)
        {
            Messages.Add(message);
            if (Gate != null && message is AdminEvent { Seq: 1 })
                await Gate.Task;
        }

        public ValueTask OnClosedAsync(AdminSession session)
        {
            Closed.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public AdminMessage Next() => Messages.TryTake(out var m, TimeSpan.FromSeconds(5)) ? m : throw new TimeoutException();
    }

    private static async Task<(AdminSession Session, FakeModule Module, RecordingHandler Handler, Task Run)> StartSessionAsync()
    {
        var (webAdmin, module, endPoint) = await ConnectAsync();
        var fake = new FakeModule(module, Key);
        var decoder = new AdminFrameDecoder();
        var handshake = AdminHandshake.RunAsync(webAdmin, decoder, Lookup, CancellationToken.None);
        Assert.True(await fake.HandshakeAsync());
        await handshake;
        var handler = new RecordingHandler();
        var session = new AdminSession(ServerId, endPoint, webAdmin, decoder, handler, NullLogger.Instance, CancellationToken.None);
        var run = session.RunAsync();
        Assert.IsType<Welcome>(handler.Next());
        Assert.IsType<Snapshot>(handler.Next());
        return (session, fake, handler, run);
    }

    [Fact]
    public async Task SequenceGapRequestsASnapshot()
    {
        var (session, module, handler, run) = await StartSessionAsync();
        await module.SendAsync(new EvRevive(1, 101, 3, -1));
        Assert.Equal(1u, ((AdminEvent)handler.Next()).Seq);

        // Seq 2 is lost: the gap is detected, the event still applied, and one RequestSnapshot sent
        await module.SendAsync(new EvRevive(3, 102, 3, -1));
        await module.SendAsync(new EvRevive(4, 103, 3, -1));
        Assert.Equal(3u, ((AdminEvent)handler.Next()).Seq);
        Assert.Equal(4u, ((AdminEvent)handler.Next()).Seq);
        var request = Assert.IsType<RequestSnapshot>(await module.ReadAsync());
        Assert.Equal(1, session.SequenceGaps);

        // The snapshot covers up to seq 5; seq 5 is then dropped as already applied and seq 6 applied
        await module.SendAsync(new Snapshot(request.RequestId, 5, 110, 4, Game, [], [], [], []));
        await module.SendAsync(new EvRevive(5, 110, 3, -1));
        await module.SendAsync(new EvRevive(6, 111, 3, -1));
        Assert.Equal(request.RequestId, Assert.IsType<Snapshot>(handler.Next()).RequestId);
        Assert.Equal(6u, ((AdminEvent)handler.Next()).Seq);

        session.Close("test");
        await run;
    }

    [Fact]
    public async Task ClosedSessionAppliesNothingMore()
    {
        // A reconnect closes the old session while its apply loop is busy; what is queued behind must not be applied
        // next to the new session's loop
        var (session, module, handler, run) = await StartSessionAsync();
        handler.Gate = new TaskCompletionSource();
        await module.SendAsync(new EvRevive(1, 101, 3, -1));
        await module.SendAsync(new EvRevive(2, 102, 3, -1));
        await module.SendAsync(new EvRevive(3, 103, 3, -1));
        Assert.Equal(1u, ((AdminEvent)handler.Next()).Seq);
        await Task.Delay(200);

        session.Close("replaced by a new link from the server");
        Assert.False(session.Closed.IsCompleted);
        handler.Gate.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(session.Closed.IsCompleted);
        Assert.Empty(handler.Messages);
    }

    [Fact]
    public async Task CommandsGetTheirRepliesAndErrors()
    {
        var (session, module, _, run) = await StartSessionAsync();

        var console = session.RequestAsync(id => new CmdConsole(id, "sv.serverName"));
        var cmd = Assert.IsType<CmdConsole>(await module.ReadAsync());
        await module.SendAsync(new ConsoleResult(cmd.RequestId, 0, ["fake"]));
        Assert.Equal(["fake"], ((ConsoleResult)await console).Lines);

        var kick = session.RequestAsync(id => new CmdKick(id, 255, "x"));
        var kickCmd = Assert.IsType<CmdKick>(await module.ReadAsync());
        await module.SendAsync(new ErrorMessage(kickCmd.RequestId, AdminErrorCode.Refused, "AI-flagged players cannot be kicked"));
        var e = await Assert.ThrowsAsync<AdminCommandException>(() => kick);
        Assert.Equal(AdminErrorCode.Refused, e.Code);

        // The module's Ping is answered with a Pong carrying its clock
        await module.SendAsync(new Ping(77, 12345));
        var pong = Assert.IsType<Pong>(await module.ReadAsync());
        Assert.Equal((77u, 12345ul), (pong.RequestId, pong.Clock));

        session.Close("test");
        await run;
    }

    [Fact]
    public async Task CommandsKeepCallOrder()
    {
        var (session, module, _, run) = await StartSessionAsync();
        await Task.Delay(200);
        Assert.True(session.IsOpen, session.CloseReason);
        session.Fire(id => new CmdSetObjectActive(id, ObjectRef.Console(29328)));
        session.Fire(id => new CmdConsole(id, "object.hasCollision 0"));
        session.Fire(id => new CmdConsole(id, "object.armor.hpLostWhileUpSideDown 0"));
        Assert.IsType<CmdSetObjectActive>(await module.ReadAsync());
        Assert.Equal("object.hasCollision 0", Assert.IsType<CmdConsole>(await module.ReadAsync()).Line);
        Assert.Equal("object.armor.hpLostWhileUpSideDown 0", Assert.IsType<CmdConsole>(await module.ReadAsync()).Line);
        session.Close("test");
        await run;
    }

    [Fact]
    public async Task NamedCommandsSendTheirConsoleLinesInOrder()
    {
        var (session, module, _, run) = await StartSessionAsync();
        await Task.Delay(200);
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddLogging().BuildServiceProvider();
        var server = new BF2WebAdmin.Server.Entities.GameServer(new BF2WebAdmin.Server.ServerInfo { ServerId = ServerId }, IPAddress.Loopback, services,
            CancellationToken.None);
        server.AttachSession(session);
        var writer = new AdminGameWriter(server, NullLogger<AdminGameWriter>.Instance);

        async Task<string> NextLine() => Assert.IsType<CmdConsole>(await module.ReadAsync()).Line;

        var heli = new BF2WebAdmin.Common.Entities.Game.Vehicle { ConsoleId = 29328, RootVehicleTemplate = "ahe_ah1z" };
        writer.SetVehicleDisabled(heli, true);
        Assert.Equal(29328u, Assert.IsType<CmdSetObjectActive>(await module.ReadAsync()).Target.Id);
        Assert.Equal("object.setIsDisabledRecursive 1", await NextLine());

        writer.SetVehicleCollision(heli, false);
        Assert.IsType<CmdSetObjectActive>(await module.ReadAsync());
        foreach (var line in BF2WebAdmin.Server.Constants.RconScript.NoclipOn)
            Assert.Equal(line, await NextLine());

        // No console id: nothing goes out
        writer.SetVehicleDisabled(new BF2WebAdmin.Common.Entities.Game.Vehicle(), true);

        writer.SetGravity(-9.81);
        Assert.Equal("physics.gravity -9.81", await NextLine());

        writer.SetTemplate("GenericProjectile", "agm114_hellfire_tv", ("drag", "0.1"), ("timeToLive", "CRD_NONE/3.5/0/0"));
        Assert.Equal("ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv", await NextLine());
        Assert.Equal("ObjectTemplate.drag 0.1", await NextLine());
        Assert.Equal("ObjectTemplate.timeToLive CRD_NONE/3.5/0/0", await NextLine());
        Assert.Throws<ArgumentException>(() => writer.SetTemplate("GenericProjectile", "agm114_hellfire_tv", ("drag 1\nquit", "0")));

        var value = writer.GetTemplateValueAsync("GenericProjectile", "agm114_hellfire_tv", "drag");
        Assert.Equal("ObjectTemplate.activeSafe GenericProjectile agm114_hellfire_tv", await NextLine());
        var get = Assert.IsType<CmdConsole>(await module.ReadAsync());
        Assert.Equal("ObjectTemplate.drag", get.Line);
        await module.SendAsync(new ConsoleResult(get.RequestId, 0, ["0.1 "]));
        Assert.Equal("0.1", await value);

        var unban = writer.RemoveBanAsync(byCdKey: false, "203.0.113.9");
        var remove = Assert.IsType<CmdConsole>(await module.ReadAsync());
        Assert.Equal("admin.removeAddressFromBanList 203.0.113.9", remove.Line);
        await module.SendAsync(new ConsoleResult(remove.RequestId, 0, []));
        await unban;

        session.Close("test");
        await run;
    }

    [Fact]
    public async Task MalformedFrameGetsError2AndCloses()
    {
        var (_, module, handler, run) = await StartSessionAsync();
        var frame = AdminProtocol.Encode(new EvRevive(1, 1, 3, -1));
        frame[6] = 1; // reserved
        await module.SendAsync(new EvRevive(1, 1, 3, -1));
        Assert.IsType<EvRevive>(handler.Next());
        await module.Stream.WriteAsync(frame);
        var error = Assert.IsType<ErrorMessage>(await module.ReadAsync());
        Assert.Equal(AdminErrorCode.Malformed, error.Code);
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        await handler.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StreamFramesAreLatestWins()
    {
        var (session, module, handler, run) = await StartSessionAsync();
        for (uint i = 0; i < 50; i++)
            await module.SendAsync(new GameStream(i, i, Game));
        await module.SendAsync(new EvRevive(1, 1, 3, -1));

        // At most 50 frames, and the event after them still arrives in order after the last applied frame
        var count = 0;
        GameStream? last = null;
        while (true)
        {
            var m = handler.Next();
            if (m is EvRevive)
                break;
            last = Assert.IsType<GameStream>(m);
            count++;
        }

        Assert.InRange(count, 1, 50);
        Assert.NotNull(last);
        session.Close("test");
        await run;
    }

    [Fact]
    public async Task AimStreamIsAcceptedWhileSubscribed()
    {
        // The 3D viewer subscribes stream 5; its frames are latest-wins like the other streams, not a protocol error
        var (session, module, handler, run) = await StartSessionAsync();
        await module.SendAsync(new AimStream(7, 0.25, [new AdminAimState(3, AdminAimState.FlagCamera, new Vec3(1, 2, 3), new Vec3(90, 0, 0), default, default)]));
        var aim = Assert.IsType<AimStream>(handler.Next());
        Assert.Equal(new Vec3(1, 2, 3), aim.Aims.Single().CameraPosition);
        Assert.True(session.IsOpen);
        session.Close("test");
        await run;
    }
}

public class AdminPlayerKeyTests
{
    private static BF2WebAdmin.Server.Entities.GameServer Server(bool isTestServer)
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var info = new BF2WebAdmin.Server.ServerInfo { ServerId = "127.0.0.1:16569", IsTestServer = isTestServer };
        return new BF2WebAdmin.Server.Entities.GameServer(info, IPAddress.Loopback, services, CancellationToken.None);
    }

    private static AdminPlayerInfo Info(string hash) => new(3, "§netsky bot", AdminPlayerInfo.FlagConnected, 0, "", hash,
        new AdminPlayerScore(0, 0, 0, 0, 0, -1),
        new AdminPlayerState(1, 0, 1, 0, 0, 0, AdminProtocol.NoConsoleId, 255, default, default, default, 1,
            AdminPlayerState.ViewInside, 0));

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", false, "0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef", true, "0123456789abcdef0123456789abcdef")]
    [InlineData("", true, "name:§netsky bot")]
    [InlineData("", false, "")]
    public void KeyIsTheHashOrOnTestServersTheName(string hash, bool isTestServer, string expected)
    {
        Assert.Equal(expected, Server(isTestServer).Applier.PlayerKey(Info(hash)));
    }
}
