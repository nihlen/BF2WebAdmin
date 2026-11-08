using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BF2WebAdmin.Server.AdminChannel;

/// <summary>
/// WebAdmin's dedicated self-signed TLS certificate for the admin channel (ECDSA P-256, 100 years). Game servers pin
/// its public key: serverext.cfg's adminCertSha256 is the SHA-256 of the SubjectPublicKeyInfo, which <see cref="Pin"/>
/// returns.
/// <para>
/// It comes from <c>ServerSettings:AdminChannel:Certificate</c> when that is set (a secret: base64 PKCS#12, or PEM text
/// with the certificate and its private key), and WebAdmin then never creates or writes one. Otherwise it is the
/// PKCS#12 file at <c>CertificatePath</c>, created on first start.
/// </para>
/// </summary>
public static class AdminCertificate
{
    public const string CertificateKey = "ServerSettings:AdminChannel:Certificate";
    private const string Subject = "CN=BF2WebAdmin admin channel";

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BF2WebAdmin", "admin-channel.pfx");

    /// <summary>The configured certificate: <see cref="AdminChannelSettings.Certificate"/> if set, else the file (created if missing).</summary>
    public static X509Certificate2 Load(AdminChannelSettings settings, ILogger logger, out string source)
    {
        if (!string.IsNullOrWhiteSpace(settings.Certificate))
        {
            source = CertificateKey;
            return FromValue(settings.Certificate, settings.CertificatePassword);
        }

        var path = string.IsNullOrWhiteSpace(settings.CertificatePath) ? DefaultPath : Environment.ExpandEnvironmentVariables(settings.CertificatePath);
        source = path;
        return LoadOrCreate(path, settings.CertificatePassword, logger);
    }

    public static X509Certificate2 LoadOrCreate(string? path, string? password, ILogger logger)
    {
        path = string.IsNullOrWhiteSpace(path) ? DefaultPath : Environment.ExpandEnvironmentVariables(path);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var created = Create();
            File.WriteAllBytes(path, created.Export(X509ContentType.Pfx, password));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            logger.LogWarning("Admin channel: created a new TLS certificate at {Path}. Every game server must pin it (adminCertSha256)", path);
        }

        var certificate = ImportPkcs12(File.ReadAllBytes(path), password);
        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException($"The admin channel certificate {path} has no private key");
        }

        return certificate;
    }

    /// <summary>
    /// The certificate from a configuration value: base64 PKCS#12 (whitespace and line breaks allowed), or PEM text with
    /// a CERTIFICATE and a PRIVATE KEY (or ENCRYPTED PRIVATE KEY, with <paramref name="password"/>) block.
    /// </summary>
    public static X509Certificate2 FromValue(string value, string? password)
    {
        X509Certificate2 certificate;
        try
        {
            if (value.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                // A PEM key is ephemeral, which Windows' TLS stack (SChannel) cannot use; going through PKCS#12 gives the
                // same key handling as the file on every platform
                using var pem = string.IsNullOrEmpty(password)
                    ? X509Certificate2.CreateFromPem(value, value)
                    : X509Certificate2.CreateFromEncryptedPem(value, value, password);
                certificate = ImportPkcs12(pem.Export(X509ContentType.Pkcs12), null);
            }
            else
            {
                certificate = ImportPkcs12(Convert.FromBase64String(value), password);
            }
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            // The message never contains the value: it is a secret
            throw new InvalidOperationException(
                $"{CertificateKey} is not a usable certificate. Expected base64 PKCS#12 (PFX), or PEM text with the certificate " +
                $"and its private key, and CertificatePassword if it is encrypted: {ex.Message}", ex);
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException($"{CertificateKey} has no private key");
        }

        return certificate;
    }

    /// <summary>A new certificate with an ephemeral key, for <see cref="LoadOrCreate"/> and <c>--admin-cert-create</c>.</summary>
    public static X509Certificate2 Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(Subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        var now = DateTimeOffset.UtcNow.AddDays(-1);
        return request.CreateSelfSigned(now, now.AddYears(100));
    }

    /// <summary>The value for <see cref="AdminChannelSettings.Certificate"/>: base64 PKCS#12 (one line), or PEM text.</summary>
    public static string Export(X509Certificate2 certificate, bool pem, string? password)
    {
        if (!pem)
            return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12, string.IsNullOrEmpty(password) ? null : password));

        using var key = certificate.GetECDsaPrivateKey() ?? throw new InvalidOperationException("The certificate has no ECDSA private key");
        var keyPem = string.IsNullOrEmpty(password)
            ? key.ExportPkcs8PrivateKeyPem()
            : key.ExportEncryptedPkcs8PrivateKeyPem(password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
        return certificate.ExportCertificatePem() + "\n" + keyPem;
    }

    /// <summary>The value for serverext.cfg's adminCertSha256: lowercase hex SHA-256 of the SubjectPublicKeyInfo.</summary>
    public static string Pin(X509Certificate2 certificate)
    {
        return Convert.ToHexString(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }

    private static X509Certificate2 ImportPkcs12(byte[] pkcs12, string? password)
    {
        // Windows' TLS stack needs the private key in a key container, not an ephemeral one. On Linux (OpenSSL) the key
        // stays in memory and nothing is written to disk
        var flags = OperatingSystem.IsWindows() ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet;
        return X509CertificateLoader.LoadPkcs12(pkcs12, password, flags);
    }
}
