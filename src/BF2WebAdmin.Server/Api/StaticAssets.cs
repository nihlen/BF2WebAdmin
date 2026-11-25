namespace BF2WebAdmin.Server.Api;

/// <summary>
/// Where the web UI loads the files that are not in this repo: the minimap images and the 3D viewer's and map builder's
/// models. Configuration section "StaticAssets" (environment: StaticAssets__MinimapBaseUrl, StaticAssets__ViewerBaseUrl).
/// Empty means the web UI's default (static.nihlen.net). A relative path such as /minimaps serves them from wwwroot.
/// </summary>
public class StaticAssetsSettings
{
    /// <summary>The folder of the minimaps: &lt;base&gt;/&lt;map in lower case&gt;.png</summary>
    public string? MinimapBaseUrl { get; set; }

    /// <summary>The folder with maps/, models/ and templates/ of the 3D viewer and the map builder</summary>
    public string? ViewerBaseUrl { get; set; }
}

/// <summary>
/// The web UI's settings from WebAdmin's configuration, read once when the UI starts (GET /api/config, without login).
/// A null base URL means the UI's own default.
/// </summary>
public record WebConfigDto(string? MinimapBaseUrl, string? ViewerBaseUrl)
{
    public static WebConfigDto From(StaticAssetsSettings settings) => new(BaseUrl(settings.MinimapBaseUrl), BaseUrl(settings.ViewerBaseUrl));

    /// <summary>Trimmed and without a trailing slash, since the UI appends "/file" ("/" is the site's root, ""); null when not set</summary>
    private static string? BaseUrl(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
}
