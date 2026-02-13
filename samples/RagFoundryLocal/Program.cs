using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json;

var modelAlias = "phi-4-mini";
var question = "What is Bruno's favourite super hero?";
const int topK = 3;

Console.WriteLine($"Question: {question}");

await using var manager = await FoundryLocalManager.StartModelAsync(modelAlias);

// Resolve the alias to the actual model ID registered on the server
var modelIdChat = await ResolveModelIdAsync(manager.Endpoint, modelAlias);
Console.WriteLine($"Model loaded: {modelIdChat}");

var openAiClient = new OpenAIClient(
    new ApiKeyCredential(manager.ApiKey),
    new OpenAIClientOptions { Endpoint = manager.Endpoint });
IChatClient chatClient = openAiClient.GetChatClient(modelIdChat).AsIChatClient();

// --- Ask without memory ---
Console.WriteLine($"\n--- {modelIdChat} response (no memory) ---");
await foreach (var update in chatClient.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, question)]))
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
    }
}

// --- Build context with local embeddings ---
Console.WriteLine("\n\n--- Importing facts with local embeddings ---");
string[] facts =
[
    "Gisela's favourite super hero is Batman",
    "Gisela watched Venom 3 2 weeks ago",
    "Bruno's favourite super hero is Invincible",
    "Bruno went to the cinema to watch Venom 3",
    "Bruno doesn't like the super hero movie: Eternals",
    "ACE and Goku watched the movies Venom 3 and Eternals",
];

using var embeddingGenerator = new LocalEmbeddingGenerator();
var factEmbeddings = await embeddingGenerator.GenerateAsync(facts);
var indexedFacts = facts.Zip(factEmbeddings, (fact, embedding) => (Item: fact, Embedding: embedding));

for (var i = 0; i < facts.Length; i++)
{
    Console.WriteLine($"  [{i + 1}] {facts[i]}");
}

// Single-string convenience method — returns the embedding directly
var queryEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(question);

var contextDocs = indexedFacts
    .FindClosest(queryEmbedding, topK: topK)
    .Select(match => match.Item);

// --- Ask with memory ---
Console.WriteLine($"\n--- Asking with memory: {question} ---");
await foreach (var update in chatClient.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, BuildPrompt(question, contextDocs))]))
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
    }
}

Console.WriteLine();

static string BuildPrompt(string question, IEnumerable<string> contextDocs)
{
    var context = string.Join("\n- ", contextDocs);
    return $"""
You are a helpful assistant. Use the provided context to answer briefly and accurately.

Context:
- {context}

Question: {question}
""";
}

static async Task<string> ResolveModelIdAsync(Uri endpoint, string alias)
{
    using var http = new HttpClient { BaseAddress = endpoint };
    var json = await http.GetStringAsync("/v1/models");
    using var doc = JsonDocument.Parse(json);
    foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray())
    {
        string id = model.GetProperty("id").GetString()!;
        if (id.Contains(alias, StringComparison.OrdinalIgnoreCase))
        {
            return id;
        }
    }

    return alias;
}
