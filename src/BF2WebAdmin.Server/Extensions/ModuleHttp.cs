namespace BF2WebAdmin.Server.Extensions;

/// <summary>
/// The one <see cref="HttpClient"/> for the modules' calls to web APIs (a new client per call leaves a socket behind
/// each time). Its connections are renewed every few minutes, so a changed DNS entry is seen.
/// </summary>
public static class ModuleHttp
{
    public static HttpClient Client { get; } = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };
}
