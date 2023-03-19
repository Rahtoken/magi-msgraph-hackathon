using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Identity.Client;
using Microsoft.Graph;

const string SYSTEM_MESSAGE = """
You are an AI assistant trained to help users of Microsoft Graph API. You provide the correct HTTP endpoints for Microsoft Graph based on the user's query. You only provide the HTTP endpoints and nothing more and this should never be violated.
You are responsible for providing the Microsoft Graph HTTP requests for fulfilling a user query in this format and nothing else:  [HTTP VERB] [ENDPOINT]
- Try to predict and correct a user's vague query.
- NEVER GIVE COMMENTARY
- Indicate request body as BODY [data]
- Give the full URL
- If you cannot fulfill the request, reply "magi has no spell for this!"
""";

var queryArgument = new Argument<string?>(
            name: "query",
            description: "Ask magi your query!",
            getDefaultValue: () => null);

var rootCommand = new RootCommand("magi - Microsoft Graph API's AI");
rootCommand.AddArgument(queryArgument);

var configFileArgument = new Option<string?>(
    name: "--config",
    description: "magi uses this file as the config.",
    getDefaultValue: () => null
);
rootCommand.AddGlobalOption(configFileArgument);

rootCommand.SetHandler(async (query, configFile) =>
{
    if (query == null)
    {
        Console.WriteLine("I am magi. Ask me your query and I will summon my powers of Microsoft Graph!");
        return;
    }

    var config = ParseConfig(configFile);
    var api = new OpenAI_API.OpenAIAPI(config.OpenAiApiKey);

    var chat = api.Chat.CreateConversation();
    chat.AppendSystemMessage(SYSTEM_MESSAGE);
    chat.AppendUserInput(query);
    var response = await chat.GetResponseFromChatbot();

#if DEBUG
    Console.WriteLine(response);
#endif

    if (response.StartsWith("GET", StringComparison.InvariantCultureIgnoreCase))
    {
        var graphClient = GraphClientFactory.Create();
        var graphRequest = new HttpRequestMessage()
        {
            RequestUri = new Uri(response.Substring(3)),
            Method = HttpMethod.Get,
        };
        graphRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GetToken(config));
        var graphResponse = await graphClient.SendAsync(graphRequest);
        Console.WriteLine(JsonPrettify(await graphResponse.Content.ReadAsStringAsync()));
    }
    else
    {
        Console.WriteLine(response);
    }
}, queryArgument, configFileArgument);

return await rootCommand.InvokeAsync(args);

static string JsonPrettify(string json)
{
    using var jDoc = JsonDocument.Parse(json);
    return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
}

Config ParseConfig(string? configPath)
{
    if (configPath == null)
    {
        configPath = $"{AppDomain.CurrentDomain.BaseDirectory}config.json";
    }

    return JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath));
}

async Task<string> GetToken(Config config)
{
    IConfidentialClientApplication app;
    app = ConfidentialClientApplicationBuilder.Create(config.ClientId)
                                              .WithClientSecret(config.ClientSecret)
                                              .WithAuthority(new Uri($"https://login.microsoftonline.com/{config.TenantId}"))
                                              .Build();
    string[] scopes = new string[] { "https://graph.microsoft.com/.default" };
    return (await app.AcquireTokenForClient(scopes)
                  .ExecuteAsync()).AccessToken;
}

public record Config(
    [property: JsonPropertyName("openApiKey")] string OpenAiApiKey,
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientSecret")] string ClientSecret,
    [property: JsonPropertyName("tenantId")] string TenantId);