using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Configuration.Models;
using Microsoft.Extensions.Options;
using Tweetinvi.Events;
using Tweetinvi.Models;
using Tweetinvi.Streaming;

namespace BF2WebAdmin.Server.Modules;

public class TwitterModule : BaseModule,
    IHandleCommandAsync<TwitterFollowCommand>,
    IHandleCommand<TwitterUnfollowCommand>
{
    private readonly IGameServer _gameServer;

    private readonly ITwitterCredentials _credentials;
    private IFilteredStream _activeStream;

    private DateTime _lastMessage = DateTime.MinValue;
    private readonly TimeSpan _messageDelay = new TimeSpan(0, 0, 10);

    public TwitterModule(IGameServer server, IOptions<TwitterConfig> config, ILogger<TwitterModule> logger, CancellationTokenSource cts) : base(server, logger, cts)
    {
        _gameServer = server;
        _credentials = new TwitterCredentials(
            config.Value.ConsumerKey,
            config.Value.ConsumerSecret,
            config.Value.AccessToken,
            config.Value.AccessTokenSecret);
    }

    public async ValueTask HandleAsync(TwitterFollowCommand command)
    {
        if (command.Stream.StartsWith("#"))
            await FollowHashTagAsync(command.Stream.Replace("#", string.Empty));

        else if (command.Stream.StartsWith("@"))
            await FollowUser(command.Stream.Replace("@", string.Empty));

        else
            await FollowKeywordAsync(command.Stream);
    }

    public void Handle(TwitterUnfollowCommand command)
    {
        _gameServer.GameWriter.SendText("Unfollowed Twitter stream");
        _activeStream?.StopStream();
    }

    private async Task FollowHashTagAsync(string hashTag)
    {
        _activeStream?.StopStream();
        _gameServer.GameWriter.SendText($"Now following §C1001#{hashTag}");
        _activeStream = Tweetinvi.Stream.CreateFilteredStream(_credentials);
        _activeStream.AddTrack($"#{hashTag}");
        _activeStream.MatchingTweetReceived += HandleTweet;
        await _activeStream.StartStreamMatchingAllConditionsAsync();
    }

    private async Task FollowKeywordAsync(string keyword)
    {
        _activeStream?.StopStream();
        _gameServer.GameWriter.SendText($"Now following §C1001{keyword}");
        _activeStream = Tweetinvi.Stream.CreateFilteredStream(_credentials);
        _activeStream.AddTrack($"{keyword}");
        _activeStream.MatchingTweetReceived += HandleTweet;
        await _activeStream.StartStreamMatchingAllConditionsAsync();
    }

    private void HandleTweet(object sender, MatchedTweetReceivedEventArgs args)
    {
        if (DateTime.UtcNow - _lastMessage < _messageDelay)
            return;
        if (ContainsLink(args.Tweet.FullText) ||
            args.Tweet.IsRetweet ||
            args.Tweet.InReplyToUserId != null)
            return;

        Logger.LogInformation("Received tweet {TweetText}", args.Tweet.FullText);
        _gameServer.GameWriter.SendText($"@{args.Tweet.CreatedBy.ScreenName} - {args.Tweet.FullText}");
        _lastMessage = DateTime.UtcNow;
    }

    private async Task FollowUser(string user)
    {
        // TODO: test
        _activeStream?.StopStream();
        _gameServer.GameWriter.SendText($"Now following §C1001@{user}");
        _activeStream = Tweetinvi.Stream.CreateFilteredStream(_credentials);
        _activeStream.AddTrack($"@{user}");
        _activeStream.MatchingTweetReceived += HandleTweet;
        await _activeStream.StartStreamMatchingAllConditionsAsync();
    }

    private bool ContainsLink(string text)
    {
        return text.Contains("http://") || text.Contains("https://");
    }
}