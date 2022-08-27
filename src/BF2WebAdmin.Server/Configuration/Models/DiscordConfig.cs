namespace BF2WebAdmin.Server.Configuration.Models;

public class DiscordConfig
{
    public IEnumerable<DiscordWebhook> Webhooks { get; set; }

    public class DiscordWebhook
    {
        public string WebhookId { get; set; }
        public string WebhookToken { get; set; }
        public string ServerGroupFilter { get; set; }
        public string MessageTypeFilter { get; set; }
    }
}