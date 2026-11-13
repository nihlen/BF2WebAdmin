using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AspNetCore.Authentication.ApiKey;
using Microsoft.AspNetCore.Authentication;

namespace BF2WebAdmin.Server.StreamBot;

/// <summary>
/// The stream bot's key (Authentication:StreamBotKey, header X-API-KEY) is a scheme of its own, apart from the admin
/// API key: the web API's policies do not list it, so the bot reaches <see cref="StreamBotHub"/> and nothing else, and
/// the admin key does not reach the hub. Without the setting no bot can connect.
/// </summary>
public static class StreamBotAuthentication
{
    public const string Scheme = "StreamBotKey";
    public const string Policy = "StreamBot";
    public const string Role = "StreamBot";

    public static AuthenticationBuilder AddStreamBotKey(this AuthenticationBuilder builder, string? streamBotKey) =>
        builder.AddApiKeyInHeader(Scheme, options =>
        {
            options.Realm = "bf2-webadmin";
            options.KeyName = "X-API-KEY";
            options.Events = new ApiKeyEvents
            {
                OnValidateKey = context =>
                {
                    if (KeyMatches(streamBotKey, context.ApiKey))
                    {
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "stream-bot", ClaimValueTypes.String, context.Options.ClaimsIssuer),
                            new Claim(ClaimTypes.Name, "stream-bot", ClaimValueTypes.String, context.Options.ClaimsIssuer),
                            new Claim(ClaimTypes.Role, Role, ClaimValueTypes.String, context.Options.ClaimsIssuer),
                        };
                        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                        context.Success();
                    }
                    else
                    {
                        context.NoResult();
                    }

                    return Task.CompletedTask;
                },
            };
        });

    public static void AddStreamBotPolicy(this Microsoft.AspNetCore.Authorization.AuthorizationOptions options) =>
        options.AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(Scheme)
            .RequireRole(Role));

    /// <summary>Exact and in fixed time; never true without a configured key.</summary>
    public static bool KeyMatches(string? expected, string? given) =>
        !string.IsNullOrWhiteSpace(expected) && given is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
}
