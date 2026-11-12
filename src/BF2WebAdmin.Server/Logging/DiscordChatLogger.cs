using System.Diagnostics;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Configuration.Models;
using Microsoft.Extensions.Options;

namespace BF2WebAdmin.Server.Logging;

public interface IChatLogger
{
    Task SendAsync(string message, string serverGroup, string messageType);
}

public class DiscordChatLogger : IChatLogger, IDisposable
{
    private readonly ILogger<DiscordChatLogger> _logger;
    private readonly IList<DiscordClientWrapper> _discordClients = new List<DiscordClientWrapper>();

    public DiscordChatLogger(IOptions<DiscordConfig> discordConfig, ILogger<DiscordChatLogger> logger)
    {
        _logger = logger;
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
                .Where(c => MatchesFilter(serverGroup, c.ServerGroupFilter) && MatchesFilter(messageType, c.MessageTypeFilter))
                .Select(c => c.Client.SendMessageAsync(message));

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed logging to Discord {Message}", message);
            activity?.SetStatus(ActivityStatusCode.Error, $"Discord message failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        foreach (var client in _discordClients)
            client.Client.Dispose();
    }

    private static bool MatchesFilter(string? value, string? filter)
    {
        return value is not null && (string.IsNullOrEmpty(filter) || value.Contains(filter));
    }

    private class DiscordClientWrapper
    {
        public DiscordClient Client { get; set; }
        public string? ServerGroupFilter { get; set; }
        public string? MessageTypeFilter { get; set; }
    }

    private class DiscordClient : IDisposable
    {
        private readonly Discord.Webhook.DiscordWebhookClient _webhookClient;

        public DiscordClient(ulong webhookId, string webhookToken)
        {
            _webhookClient = new Discord.Webhook.DiscordWebhookClient(webhookId, webhookToken);
        }

        public async Task SendMessageAsync(string text)
        {
            await _webhookClient.SendMessageAsync(text);
        }

        public void Dispose()
        {
            _webhookClient.Dispose();
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