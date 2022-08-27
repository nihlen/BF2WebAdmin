using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Configuration.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BF2WebAdmin.Server.Modules;

public class QuoteModule : IModule,
    IHandleCommandAsync<QuoteCommand>,
    IHandleCommandAsync<QuoteCategoryCommand>
{
    private readonly IGameServer _gameServer;
    private readonly MashapeConfig _config;

    public QuoteModule(IGameServer server, IOptions<MashapeConfig> config)
    {
        _gameServer = server;
        _config = config.Value;
    }

    public async ValueTask HandleAsync(QuoteCommand command)
    {
        var quote = await GetQuoteAsync("movies");
        _gameServer.GameWriter.SendText(quote);
    }

    public async ValueTask HandleAsync(QuoteCategoryCommand command)
    {
        if (command.Category != "movies" && command.Category != "famous")
        {
            _gameServer.GameWriter.SendText($"Unknown category '{command.Category}'");
            return;
        }

        var quote = await GetQuoteAsync(command.Category);
        _gameServer.GameWriter.SendText(quote);
    }

    private async Task<string> GetQuoteAsync(string category)
    {
        category = Uri.EscapeDataString(category);

        var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Add("X-Mashape-Key", _config.Key);

        var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("cat", category) });
        var response = await client.PostAsync("https://andruxnet-random-famous-quotes.p.mashape.com/", content);

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<QuoteResult>(responseJson);

        return $"\"{result.Quote}\" - {result.Author}";
    }

    public class QuoteResult
    {
        [JsonProperty("quote")]
        public string Quote { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("cat")]
        public string Cat { get; set; }
    }
}