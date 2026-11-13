using Microsoft.AspNetCore.Mvc;

namespace BF2WebAdmin.Server.Controllers;

/// <summary>Login and logout are web API endpoints (Api/ApiEndpoints.cs).</summary>
public class AuthController : Controller
{
    [HttpGet("/test")]
    public IActionResult Test()
    {
        return Ok("alive");
    }
}

public class AuthSettings
{
    public IEnumerable<AdminUser> Admins { get; set; }
    public string ApiKey { get; set; }

    /// <summary>The stream bot's key, for its hub only (StreamBot/StreamBotAuthentication.cs); without it no bot can connect.</summary>
    public string? StreamBotKey { get; set; }
}

public class AdminUser
{
    public string Username { get; set; }
    public string Password { get; set; }
}
