using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        var response = await ModuleHttp.Client.GetStringAsync($"https://api.urbandictionary.com/v0/define?term={term}");
        var result = JsonSerializer.Deserialize<UrbanDictionaryResult>(response);
        var definition = result.List.FirstOrDefault();
        if (definition != null)
            _gameServer.GameWriter.SendText($"§C1001{definition.Word}: §C1001{definition.Definition}");
        else
            _gameServer.GameWriter.SendText($"No definition found");
    }

    public class UrbanDictionaryResult
    {
        [JsonPropertyName("result_type")]
        public string ResultType { get; set; }

        [JsonPropertyName("list")]
        public IEnumerable<UrbanDictionaryDefinition> List { get; set; }
    }

    public class UrbanDictionaryDefinition
    {
        [JsonPropertyName("defid")]
        public int Defid { get; set; }

        [JsonPropertyName("word")]
        public string Word { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("definition")]
        public string Definition { get; set; }

        [JsonPropertyName("example")]
        public string Example { get; set; }

        [JsonPropertyName("thumbs_up")]
        public int ThumbsUp { get; set; }

        [JsonPropertyName("thumbs_down")]
        public int ThumbsDown { get; set; }
    }
}