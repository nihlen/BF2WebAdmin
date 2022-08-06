using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BF2WebAdmin.Server.Controllers;

public class AuthController : Controller
{
    private readonly AuthSettings _authSettings;

    public AuthController(IOptions<AuthSettings> authSettings)
    {
        _authSettings = authSettings.Value;
    }

    [HttpPost("/api/login")]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        var user = _authSettings.Admins.FirstOrDefault(a => a.Username == username);
        if (user is null)
            return BadRequest();

        if (!user.Password.Equals(password, StringComparison.Ordinal))
            return BadRequest();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.NameIdentifier, username),
            new(ClaimTypes.Role, "Administrator"),
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            //AllowRefresh = <bool>,
            // Refreshing the authentication session should be allowed.

            //ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10),
            // The time at which the authentication ticket expires. A 
            // value set here overrides the ExpireTimeSpan option of 
            // CookieAuthenticationOptions set with AddCookie.

            //IsPersistent = true,
            // Whether the authentication session is persisted across 
            // multiple requests. When used with cookies, controls
            // whether the cookie's lifetime is absolute (matching the
            // lifetime of the authentication ticket) or session-based.

            //IssuedUtc = <DateTimeOffset>,
            // The time at which the authentication ticket was issued.

            //RedirectUri = <string>
            // The full path or absolute URI to be used as an http 
            // redirect response value.
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        return Redirect(returnUrl ?? "/");
    }

    [HttpPost("/api/logout")]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Redirect(returnUrl ?? "/");
    }

    [HttpGet("/test")]
    public async Task<IActionResult> Test()
    {
        return Ok("alive");
    }
}

public class AuthSettings
{
    public IEnumerable<AdminUser> Admins { get; set; }
}

public class AdminUser
{
    public string Username { get; set; }
    public string Password { get; set; }
}
