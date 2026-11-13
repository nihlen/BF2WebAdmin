using System.Security.Claims;
using AspNetCore.Authentication.ApiKey;
using BF2WebAdmin.Server.StreamBot;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace BF2WebAdmin.Server.Api;

/// <summary>
/// The admin API key (Authentication:ApiKey, header X-API-KEY) and <see cref="ApiEndpoints.AdminPolicy"/>, which takes
/// a login cookie or that key. The stream bot's key is another scheme (<see cref="StreamBotAuthentication"/>) that this
/// policy does not list.
/// </summary>
public static class AdminApiKeyAuthentication
{
    public static AuthenticationBuilder AddAdminApiKey(this AuthenticationBuilder builder, string? apiKey) =>
        builder.AddApiKeyInHeader(ApiKeyDefaults.AuthenticationScheme, options =>
        {
            options.Realm = "bf2-webadmin";
            options.KeyName = "X-API-KEY";
            options.Events = new ApiKeyEvents
            {
                OnValidateKey = context =>
                {
                    // Exact and in fixed time, like the password (it was case-insensitive)
                    if (StreamBotAuthentication.KeyMatches(apiKey, context.ApiKey))
                    {
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, "api-user", ClaimValueTypes.String, context.Options.ClaimsIssuer),
                            new Claim(ClaimTypes.Name, "api-user", ClaimValueTypes.String, context.Options.ClaimsIssuer),
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

    public static void AddAdminPolicy(this AuthorizationOptions options) =>
        options.AddPolicy(ApiEndpoints.AdminPolicy, policy => policy
            .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, ApiKeyDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser());
}
