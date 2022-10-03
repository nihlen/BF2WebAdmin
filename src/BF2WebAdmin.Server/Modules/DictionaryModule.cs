using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using Newtonsoft.Json;

namespace BF2WebAdmin.Server.Modules;

public class DictionaryModule : IModule,
    IHandleCommandAsync<DefineCommand>
{
    private readonly IGameServer _gameServer;

    public DictionaryModule(IGameServer server)
    {
        _gameServer = server;
    }

    public async ValueTask HandleAsync(DefineCommand command)
    {
        var term = Uri.EscapeDataString(command.Term);
        var client = new HttpClient();
        var response = await client.GetStringAsync($"https://api.urbandictionary.com/v0/define?term={term}");
        var result = JsonConvert.DeserializeObject<UrbanDictionaryResult>(response);
        var definition = result.List.FirstOrDefault();
        if (definition != null)
            _gameServer.GameWriter.SendText($"§C1001{definition.Word}: §C1001{definition.Definition}");
        else
            _gameServer.GameWriter.SendText($"No definition found");
    }

    public class UrbanDictionaryResult
    {
        [JsonProperty("result_type")]
        public string ResultType { get; set; }

        [JsonProperty("list")]
        public IEnumerable<UrbanDictionaryDefinition> List { get; set; }
    }

    public class UrbanDictionaryDefinition
    {
        [JsonProperty("defid")]
        public int Defid { get; set; }

        [JsonProperty("word")]
        public string Word { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("definition")]
        public string Definition { get; set; }

        [JsonProperty("example")]
        public string Example { get; set; }

        [JsonProperty("thumbs_up")]
        public int ThumbsUp { get; set; }

        [JsonProperty("thumbs_down")]
        public int ThumbsDown { get; set; }
    }
}