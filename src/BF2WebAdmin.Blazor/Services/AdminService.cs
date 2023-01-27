using System.Net;
using BF2WebAdmin.Shared.Communication.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace BF2WebAdmin.Blazor.Services;

public class AdminService : IAsyncDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly HttpClient _httpClient;

    public HubConnection HubConnection { get; private set; }
    public string? SelectedServerId { get; private set; }

    private bool _isAuthenticated;

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            _isAuthenticated = value;
            OnIsAuthenticatedChange?.Invoke(value);
        }
    }

    public event Action<bool>? OnIsAuthenticatedChange;

    public AdminService(NavigationManager navigationManager, HttpClient httpClient)
    {
        _navigationManager = navigationManager;
        _httpClient = httpClient;
        HubConnection = new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/hubs/server"))
            .WithAutomaticReconnect()
            .AddMessagePackProtocol()

            // .AddJsonProtocol(options =>
            // {
            //     options.PayloadSerializerOptions.PropertyNamingPolicy = null;
            // })
            .Build();
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var loginUrl = new Uri($"/api/login?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}", UriKind.Relative);
        var response = await _httpClient.PostAsync(loginUrl, null);
        IsAuthenticated = response.IsSuccessStatusCode;
        return response.IsSuccessStatusCode;
    }

    public async Task ConnectAsync()
    {
        if (HubConnection.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            return;

        try
        {
            if (HubConnection.State == HubConnectionState.Disconnected)
                await HubConnection.StartAsync();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
                _navigationManager.NavigateTo("/login", forceLoad: false);

            return;
        }

        IsAuthenticated = true;

        // Just used to get server data atm
        await HubConnection.SendAsync("UserConnect");
    }

    public async Task SelectServerAsync(string? serverId)
    {
        if (serverId is null)
            return;

        serverId = UrlDecodeServerId(serverId);
        SelectedServerId = serverId;
        await HubConnection.SendAsync("SelectServer", serverId);
    }

    public async Task DeselectServerAsync(string? serverId)
    {
        if (serverId is null)
            return;

        serverId = UrlDecodeServerId(serverId);
        if (SelectedServerId == serverId)
        {
            SelectedServerId = null;
        }

        await HubConnection.SendAsync("DeselectServer", serverId);
    }

    public async Task SendChatMessageAsync(string text) =>
        await HubConnection.SendAsync("SendChatMessage", SelectedServerId, text);

    public async Task SendRconCommandAsync(string text) =>
        await HubConnection.SendAsync("SendRconCommand", SelectedServerId, text);

    public async Task SendCustomCommandAsync(string text) =>
        await HubConnection.SendAsync("SendCustomCommand", SelectedServerId, text);

    // TODO: new ServerData type? should we send it in an event instead so it works for many active users?
    public async Task<ServerDataDto> GetServerAsync(string? serverId = null) =>
        await HubConnection.InvokeAsync<ServerDataDto>("GetServer", serverId ?? SelectedServerId);

    public async Task SetServerAsync(ServerDataDto server) =>
        await HubConnection.SendAsync("SetServer", server);

    public async Task<IEnumerable<TestServerResult>> TestServerAsync(ServerDataDto server) =>
        await HubConnection.InvokeAsync<TestServerResult[]>("TestServer", server);

    public async Task RemoveServerAsync(string serverId) =>
        await HubConnection.SendAsync("RemoveServer", serverId);

    public async Task<IEnumerable<string>> GetServerGroupModulesAsync(string serverGroup) => 
        await HubConnection.InvokeAsync<string[]>("GetServerGroupModules", serverGroup);

    public async Task SetServerGroupModulesAsync(string serverGroup, string[] moduleNames) =>
        await HubConnection.SendAsync("SetServerGroupModules", serverGroup, moduleNames);

    public async Task ReloadServerGroupModulesAsync(string serverGroup) =>
        await HubConnection.SendAsync("ReloadServerGroupModules", serverGroup);

    public async Task<IEnumerable<string>> GetAllModulesAsync() =>
        await HubConnection.InvokeAsync<string[]>("GetAllModules");

    public async ValueTask DisposeAsync() =>
        await HubConnection.DisposeAsync();

    public static string? UrlEncodeServerId(string? serverId) =>
        serverId?.Replace(".", "-")?.Replace(":", "_");

    public static string? UrlDecodeServerId(string? encodedServerId) =>
        encodedServerId?.Replace("-", ".")?.Replace("_", ":");
}
