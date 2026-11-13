using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Services;
using Discord.WebSocket;
using DiscordConfig = BF2WebAdmin.Server.Configuration.Models.DiscordConfig;
using MessageType = BF2WebAdmin.Common.Entities.Game.MessageType;

namespace BF2WebAdmin.Server.Modules.DiscordBot;

/// <summary>
/// What people write in a bot's admin channel: help, the admin commands of <see cref="ServerAdmin"/> ("!" or "."), chat
/// commands (".switch", run as a Discord player at Admin, God for <see cref="DiscordConfig.GodUsers"/>), console lines
/// ("!" and anything else), /score, and plain text, which goes to the game's chat. Runs on Discord's threads.
/// </summary>
public sealed class DiscordCommandHandler(IGameServer game, ServerAdmin admin, DiscordConfig discordConfig)
{

    public async Task HandleAsync(SocketMessage message, DiscordConnection connection)
    {
        var content = message.Content;
        var by = (message.Author as SocketGuildUser)?.Nickname ?? message.Author.Username;

        if (content.Length > 4 && content[1..5] == "help")
        {
            connection.Reply(message.Channel, DiscordText.Help);
        }
        // Both prefixes work for both kinds: the admin commands (".kick" too) answer here, "!switch" runs the chat
        // command, and a "!" line that is neither is a console line
        else if (ServerAdmin.IsAdminCommand(content) && (content.StartsWith('!') || content.StartsWith('.'))
                 || (content.StartsWith('!') && !game.ModManager.IsCommand(WebModule.Alias(content))))
        {
            string response;
            try
            {
                response = await admin.RunAsync(content, by);
            }
            catch (AdminCommandException e)
            {
                response = ServerAdmin.Describe(e);
            }

            connection.Reply(message.Channel, $"```{BaseModule.GetObfuscatedResponse(response)}```");
        }
        else if (content.StartsWith('.') || content.StartsWith('!'))
        {
            await game.ModManager.HandleFakeChatMessageAsync(new Message
            {
                Channel = ChatChannel.Global,
                Type = MessageType.Player,
                Player = new Player
                {
                    Index = -1,
                    Name = by,
                    Hash = discordConfig.GodUsers.Contains(message.Author.Id) ? DiscordModule.DiscordBotHashGod : DiscordModule.DiscordBotHashAdmin
                },
                Text = content
            });
        }
        else if (content.StartsWith('/'))
        {
            if (content.ToLower() == "/score")
                connection.Reply(message.Channel, DiscordText.ScoreTable(game));
        }
        else
        {
            game.GameWriter.SendText($"[§C1001{by}§C1001] {content}", false, false);
        }
    }
}
