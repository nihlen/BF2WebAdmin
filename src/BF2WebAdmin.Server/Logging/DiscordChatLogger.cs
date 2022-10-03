using System.Diagnostics;
using BF2WebAdmin.Server.Configuration.Models;
using Microsoft.Extensions.Options;
using Nihlen.Common.Telemetry;
using Serilog;

namespace BF2WebAdmin.Server.Logging;

public interface IChatLogger
{
    Task SendAsync(string message, string serverGroup, string messageType);
}

public class DiscordChatLogger : IChatLogger
{
    private readonly IList<DiscordClientWrapper> _discordClients = new List<DiscordClientWrapper>();

    public DiscordChatLogger(IOptions<DiscordConfig> discordConfig)
    {
        foreach (var discordWebhook in discordConfig.Value.Webhooks)
        {
            _discordClients.Add(new DiscordClientWrapper
            {
                Client = new DiscordClient(ulong.Parse(discordWebhook.WebhookId), discordWebhook.WebhookToken),
                ServerGroupFilter = discordWebhook.ServerGroupFilter,
                MessageTypeFilter = discordWebhook.MessageTypeFilter
            });
        }
    }

    public async Task SendAsync(string message, string serverGroup, string messageType)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("SendDiscordMessage");
        
        try
        {
            var tasks = _discordClients
                .Where(c => (serverGroup?.Contains(c.ServerGroupFilter) ?? false) && (messageType?.Contains(c.MessageTypeFilter) ?? false))
                .Select(c => c.Client.SendMessageAsync(message));

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed logging to Discord {Message}", message);
            activity?.SetStatus(ActivityStatusCode.Error, $"Discord message failed: {ex.Message}");
        }
    }

    private class DiscordClientWrapper
    {
        public DiscordClient Client { get; set; }
        public string ServerGroupFilter { get; set; }
        public string MessageTypeFilter { get; set; }
    }

    private class DiscordClient : IDisposable
    {
        private ulong _webhookId;
        private string _webhookToken;
        private Discord.Webhook.DiscordWebhookClient _webhookClient;
        private Discord.WebSocket.DiscordSocketClient _websocketClient;

        public DiscordClient(ulong webhookId, string webhookToken)
        {
            _webhookId = webhookId;
            _webhookToken = webhookToken;
            _webhookClient = new Discord.Webhook.DiscordWebhookClient(_webhookId, _webhookToken);
            _websocketClient = new Discord.WebSocket.DiscordSocketClient();
        }

        public async Task SendMessageAsync(string text)
        {
            await _webhookClient.SendMessageAsync(text);
        }

        public void Dispose()
        {
            _websocketClient?.Dispose();
        }
    }
}

public class FakeChatLogger : IChatLogger
{
    public Task SendAsync(string message, string serverGroup, string messageType)
    {
        return Task.CompletedTask;
    }
}