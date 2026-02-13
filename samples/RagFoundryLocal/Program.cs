using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using OpenAI;
using RagFoundryLocal;
using System.ClientModel;

Console.WriteLine("RAG sample with ElBruno.LocalEmbeddings + Foundry Local phi4-mini");
Console.WriteLine("Type 'exit' to quit.");

using var embeddingGenerator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());
var vectorStore = new SimpleVectorStore(embeddingGenerator);
await vectorStore.AddAsync(KnowledgeBase.Documents);

await using var manager = await FoundryLocalManager.StartModelAsync("phi4-mini");
var openAiClient = new OpenAIClient(
    new ApiKeyCredential(manager.ApiKey),
    new OpenAIClientOptions { Endpoint = manager.Endpoint });
IChatClient chatClient = openAiClient.GetChatClient("phi4-mini").AsIChatClient();

while (true)
{
    Console.Write("\nYou> ");
    var query = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(query))
    {
        continue;
    }

    if (query.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    var contextDocs = await vectorStore.SearchAsync(query, topK: 3);
    var prompt = BuildPrompt(query, contextDocs.Select(d => d.Content));

    Console.Write("Assistant> ");
    await foreach (var update in chatClient.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, prompt)]))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            Console.Write(update.Text);
        }
    }

    Console.WriteLine();
}

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
