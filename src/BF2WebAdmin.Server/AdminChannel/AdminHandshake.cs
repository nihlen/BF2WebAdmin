using System.Security.Cryptography;

namespace BF2WebAdmin.Server.AdminChannel;

public sealed record AdminHandshakeResult(string ServerId, Hello Hello);

/// <summary>
/// Hello / Challenge / Auth, WebAdmin's side. WebAdmin proves the key first (Pw); the module then sends
/// Auth (Pm), Welcome and Snapshot without waiting, so the decoder may hold more frames when this returns.
/// </summary>
public static class AdminHandshake
{
    /// <summary>Returns the raw key for a server id, or null for an unknown id or a server without a key.</summary>
    public delegate ValueTask<byte[]?> KeyLookup(string serverId);

    public static async Task<AdminHandshakeResult> RunAsync(Stream stream, AdminFrameDecoder decoder, KeyLookup lookupKey, CancellationToken token)
    {
        var hello = await ReadAsync(stream, decoder, token) switch
        {
            Hello h => h,
            ErrorMessage e => throw new AdminHandshakeException($"the module sent Error {e.Code} instead of Hello: {e.Message}"),
            var m => await FailAsync<Hello>(stream, AdminErrorCode.UnknownType, $"expected Hello, got {m.Type}", token),
        };

        if (hello.Version != AdminProtocol.Version)
            await FailAsync<Hello>(stream, AdminErrorCode.VersionMismatch, $"admin protocol version {hello.Version} is not supported (1)", token);

        var key = await lookupKey(hello.ServerId);
        if (key == null)
            await FailAsync<Hello>(stream, AdminErrorCode.AuthenticationFailed, $"unknown server id or no admin key: '{hello.ServerId}'", token);

        var webAdminNonce = RandomNumberGenerator.GetBytes(AdminProtocol.NonceSize);
        var pw = AdminProtocol.Proof(key, AdminProtocol.WebAdminProofLabel, hello.ServerId, hello.Nonce, webAdminNonce);
        await WriteAsync(stream, new Challenge(webAdminNonce, pw), token);

        var auth = await ReadAsync(stream, decoder, token) switch
        {
            Auth a => a,
            ErrorMessage e => throw new AdminHandshakeException(e.Code == AdminErrorCode.AuthenticationFailed
                ? $"the module refused WebAdmin's proof (adminKey differs from the Server.AdminKey of '{hello.ServerId}'): {e.Message}"
                : $"the module sent Error {e.Code}: {e.Message}"),
            var m => await FailAsync<Auth>(stream, AdminErrorCode.UnknownType, $"expected Auth, got {m.Type}", token),
        };

        var pm = AdminProtocol.Proof(key, AdminProtocol.ServerProofLabel, hello.ServerId, hello.Nonce, webAdminNonce);
        if (!CryptographicOperations.FixedTimeEquals(pm, auth.Proof))
            await FailAsync<Auth>(stream, AdminErrorCode.AuthenticationFailed, "the server's proof does not match its admin key", token);

        return new AdminHandshakeResult(hello.ServerId, hello);
    }

    /// <summary>Parses a key as stored in Server.AdminKey and serverext.cfg's adminKey: 32-128 hex digits.</summary>
    public static byte[]? ParseKey(string? hex)
    {
        hex = hex?.Trim();
        if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0 || hex.Length < 32 || hex.Length > 128)
            return null;

        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static async Task<AdminMessage> ReadAsync(Stream stream, AdminFrameDecoder decoder, CancellationToken token)
    {
        var buffer = new byte[4096];
        while (true)
        {
            if (decoder.TryRead(out var type, out var payload))
                return AdminProtocol.Decode(type, payload);

            var n = await stream.ReadAsync(buffer, token);
            if (n == 0)
                throw new AdminHandshakeException("the module closed the connection during the handshake");

            decoder.Append(buffer.AsSpan(0, n));
        }
    }

    private static async Task WriteAsync(Stream stream, AdminMessage message, CancellationToken token)
    {
        await stream.WriteAsync(AdminProtocol.Encode(message), token);
        await stream.FlushAsync(token);
    }

    private static async Task<T> FailAsync<T>(Stream stream, AdminErrorCode code, string message, CancellationToken token)
    {
        try
        {
            await WriteAsync(stream, new ErrorMessage(0, code, message), token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
        }

        throw new AdminHandshakeException(message, code);
    }
}

public class AdminHandshakeException(string message, AdminErrorCode? code = null) : Exception(message)
{
    public AdminErrorCode? Code { get; } = code;
}
