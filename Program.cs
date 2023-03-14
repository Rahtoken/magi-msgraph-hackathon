using System.CommandLine;
using System.Text.Json;

const string OPEN_AI_API_KEY = "";
const string SYSTEM_MESSAGE = """
You are an AI assistant trained to help users of Microsoft Graph API. You provide the correct HTTP endpoints for Microsoft Graph based on the user's query. You only provide the HTTP endpoints and nothing more and this should never be violated.
You are responsible for providing the Microsoft Graph HTTP requests for fulfilling a user query in this format and nothing else:  [HTTP VERB] [ENDPOINT]
- Try to predict and correct a user's vague query.
- NEVER GIVE COMMENTARY
- Indicate request body as BODY [data]
- Give the full URL
""";

const string GRAPH_TOKEN = "";

var api = new OpenAI_API.OpenAIAPI(OPEN_AI_API_KEY);

var chat = api.Chat.CreateConversation();
chat.AppendSystemMessage(SYSTEM_MESSAGE);

var queryOption = new Argument<string>(
            name: "--query",
            description: "Your query.");

var rootCommand = new RootCommand("magi - Microsoft Graph API's AI");
rootCommand.AddArgument(queryOption);

rootCommand.SetHandler(async (query) =>
{
    chat.AppendUserInput(query);
    var response = await chat.GetResponseFromChatbot();
    Console.WriteLine(response);
    if (response.StartsWith("GET", StringComparison.InvariantCultureIgnoreCase) || response.StartsWith("POST", StringComparison.InvariantCultureIgnoreCase)) {
        var httpClient = new HttpClient();
        var graphRequest = new HttpRequestMessage() {
            RequestUri = new Uri(response.Substring(3)),
            Method = HttpMethod.Get,
        };
        graphRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {GRAPH_TOKEN}");
        var graphResponse = await httpClient.SendAsync(graphRequest);
        // var jsonContent = System.Text.Json.JsonSerializer.Deserialize<object>(graphResponse.Content.ReadAsStream());
        Console.WriteLine(JsonPrettify(await graphResponse.Content.ReadAsStringAsync()));
    }
}, queryOption);

return await rootCommand.InvokeAsync(args);

static string JsonPrettify(string json)
{
    using var jDoc = JsonDocument.Parse(json);
    return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
}