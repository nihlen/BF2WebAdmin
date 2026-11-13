using System;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Data.Abstractions;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Modules.DiscordBot;
using BF2WebAdmin.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using DiscordConfig = BF2WebAdmin.Server.Configuration.Models.DiscordConfig;

namespace BF2WebAdmin.Tests;

/// <summary>The Discord bot's text and a server without a bot.</summary>
public class DiscordModuleTests
{
    [Theory]
    [InlineData("ahe_z10_hj8launcher_tv", "Z-10 TV")]
    [InlineData("AHE_AH1Z_GUN", "AH-1Z MG")]
    [InlineData("unknown_weapon", "unknown_weapon")]
    public void WeaponName(string weapon, string expected) => Assert.Equal(expected, DiscordText.WeaponName(weapon));

    [Fact]
    public void Sanitize_BreaksMentionsAndMarkdown()
    {
        const string zws = "​";
        Assert.Equal($"@{zws}everyone", DiscordText.Sanitize("@everyone"));
        Assert.Equal($@"<{zws}@{zws}123\>", DiscordText.Sanitize("<@123>")); // Format.Sanitize escapes > too
        Assert.Equal(@"\*bold\*", DiscordText.Sanitize("*bold*"));
        Assert.Equal(string.Empty, DiscordText.Sanitize(null!));
        Assert.Equal(":flag_cn:", DiscordText.TeamFlag("CH"));
    }

    [Fact]
    public async Task WithoutABotToken_NothingIsSentAndNothingStarts()
    {
        var server = Substitute.For<IGameServer>();
        server.ServerInfo.Returns(new ServerInfo());
        var taskRunner = Substitute.For<ITaskRunner>();
        var module = new DiscordModule(server, Substitute.For<IGameStreamService>(), taskRunner, Substitute.For<IServerBanRepository>(),
            Options.Create(new DiscordConfig()), NullLogger<DiscordModule>.Instance, new CancellationTokenSource());

        await module.SendTextMessageToChannelsAsync("text");
        await module.HandleAsync(new MapChangedEvent(new Map { Name = "dalian_2_v_2" }, [], DateTimeOffset.UtcNow));
        await module.DisposeAsync();

        taskRunner.DidNotReceiveWithAnyArgs().RunBackgroundTask(default!, default!, default);
    }
}
