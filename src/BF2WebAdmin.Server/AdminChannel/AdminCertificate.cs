using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// WebAdmin's dedicated self-signed TLS certificate for the admin channel. It is generated once
/// (ECDSA P-256, 100 years) and kept outside the repository. Game servers pin its public key:
/// serverext.cfg's adminCertSha256 is the SHA-256 of the SubjectPublicKeyInfo, which <see cref="Pin"/> returns.
/// </summary>
public static class AdminCertificate
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BF2WebAdmin", "admin-channel.pfx");

    public static X509Certificate2 LoadOrCreate(string? path, string? password, ILogger logger)
    {
        path = string.IsNullOrWhiteSpace(path) ? DefaultPath : Environment.ExpandEnvironmentVariables(path);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=BF2WebAdmin admin channel", key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            var now = DateTimeOffset.UtcNow.AddDays(-1);
            using var created = request.CreateSelfSigned(now, now.AddYears(100));
            File.WriteAllBytes(path, created.Export(X509ContentType.Pfx, password));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            logger.LogWarning("Admin channel: created a new TLS certificate at {Path}. Every game server must pin it (adminCertSha256)", path);
        }

        // Windows' TLS stack needs the private key in a key container, not an ephemeral one
        var flags = OperatingSystem.IsWindows() ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet;
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, flags);
        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException($"The admin channel certificate {path} has no private key");

        return certificate;
    }

    /// <summary>The value for serverext.cfg's adminCertSha256: lowercase hex SHA-256 of the SubjectPublicKeyInfo.</summary>
    public static string Pin(X509Certificate2 certificate)
    {
        return Convert.ToHexString(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }
}
