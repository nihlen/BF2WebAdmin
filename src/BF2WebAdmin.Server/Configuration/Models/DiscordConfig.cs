namespace BF2WebAdmin.Server.Configuration.Models;

public class DiscordConfig
{
    /// <summary>The Discord user that ran WebAdmin's commands at God level before this was a setting.</summary>
    public const ulong DefaultGodUserId = 135810577292328960;

    public IEnumerable<DiscordWebhook> Webhooks { get; set; }

    /// <summary>
    /// Discord users whose "." and "!" chat commands in a bot's admin channel run at God level; everyone else there runs
    /// them at Admin level. Unset: <see cref="DefaultGodUserId"/>. (Nullable, because the configuration binder appends a
    /// configured list to a default one.)
    /// </summary>
    public ulong[]? GodUserIds { get; set; }

    /// <summary>The guild (Discord server) whose channels get the bots' debug messages: those whose name contains it.</summary>
    public string DebugGuildName { get; set; } = "netsky";

    public IReadOnlyCollection<ulong> GodUsers => GodUserIds ?? [DefaultGodUserId];

    public class DiscordWebhook
    {
        public string WebhookId { get; set; }
        public string WebhookToken { get; set; }
        public string ServerGroupFilter { get; set; }
        public string MessageTypeFilter { get; set; }
    }
}
