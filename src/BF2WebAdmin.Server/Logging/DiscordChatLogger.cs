using System.Diagnostics;
using System.Threading.Channels;
using BF2WebAdmin.Common.External;
using BF2WebAdmin.Server.Configuration.Models;
using Microsoft.Extensions.Options;

namespace BF2WebAdmin.Server.Logging;

public interface IChatLogger
{
    Task SendAsync(string message, string serverGroup, string messageType);
}

/// <summary>
/// Posts to the Discord webhooks whose filters match. <see cref="SendAsync"/> only queues the message: the event
/// handlers that call it (joins, leaves) run on the admin channel's apply loop, which must not wait for Discord. A
/// background loop sends the queue in order; when Discord is down the oldest messages are dropped.
/// </summary>
public class DiscordChatLogger : IChatLogger, IDisposable, IAsyncDisposable
{
    private const int QueueLength = 500;

    private readonly ILogger<DiscordChatLogger> _logger;
    private readonly IList<DiscordClientWrapper> _discordClients = new List<DiscordClientWrapper>();
    private readonly Channel<(string Message, string ServerGroup, string MessageType)> _queue =
        Channel.CreateBounded<(string, string, string)>(new BoundedChannelOptions(QueueLength) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Task _sender;

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

        _sender = Task.Run(SendQueuedAsync);
    }

    public Task SendAsync(string message, string serverGroup, string messageType)
    {
        _queue.Writer.TryWrite((message, serverGroup, messageType));
        return Task.CompletedTask;
    }

    private async Task SendQueuedAsync()
    {
        await foreach (var (message, serverGroup, messageType) in _queue.Reader.ReadAllAsync())
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
    }

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Sends what is queued (for up to 5 s), then closes the webhook clients.</summary>
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await Task.WhenAny(_sender, Task.Delay(DrainTimeout));
        DisposeClients();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _sender.Wait(DrainTimeout);
        DisposeClients();
        GC.SuppressFinalize(this);
    }

    private void DisposeClients()
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