using System;
using BF2WebAdmin.Server.Api;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>GET /api/config: the web UI's bases for the minimaps and the viewer's models (StaticAssets).</summary>
public class StaticAssetsTests
{
    [Fact]
    public void UnsetOrBlankBasesAreNull_SoTheUiUsesItsDefaults()
    {
        Assert.Equal(new WebConfigDto(null, null), WebConfigDto.From(new StaticAssetsSettings()));
        Assert.Equal(new WebConfigDto(null, null), WebConfigDto.From(new StaticAssetsSettings { MinimapBaseUrl = "", ViewerBaseUrl = "  " }));
    }

    [Fact]
    public void BasesAreTrimmedAndLoseTheirTrailingSlash()
    {
        var config = WebConfigDto.From(new StaticAssetsSettings { MinimapBaseUrl = " https://cdn.example.org/minimaps/ ", ViewerBaseUrl = "/viewer/v1" });

        Assert.Equal("https://cdn.example.org/minimaps", config.MinimapBaseUrl);
        Assert.Equal("/viewer/v1", config.ViewerBaseUrl);
        // The site's root
        Assert.Equal("", WebConfigDto.From(new StaticAssetsSettings { MinimapBaseUrl = "/" }).MinimapBaseUrl);
    }

    [Fact]
    public void EnvironmentVariablesBindToTheSection()
    {
        // The same mapping as Program's AddEnvironmentVariables (__ for :), under a prefix so no other test sees it
        const string prefix = "BF2WA_TEST_STATICASSETS_";
        Environment.SetEnvironmentVariable(prefix + "StaticAssets__MinimapBaseUrl", "/minimaps");
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            var settings = configuration.GetSection("StaticAssets").Get<StaticAssetsSettings>();

            Assert.NotNull(settings);
            Assert.Equal("/minimaps", WebConfigDto.From(settings).MinimapBaseUrl);
            Assert.Null(settings.ViewerBaseUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "StaticAssets__MinimapBaseUrl", null);
        }
    }
}
