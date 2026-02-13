using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

var modelIdChat = "phi4-mini";
var question = "What is the default model in LocalEmbeddings?";
const int topK = 3;

Console.WriteLine($"Question: {question}");

await using var manager = await FoundryLocalManager.StartModelAsync(modelIdChat);
var openAiClient = new OpenAIClient(
    new ApiKeyCredential(manager.ApiKey),
    new OpenAIClientOptions { Endpoint = manager.Endpoint });
IChatClient chatClient = openAiClient.GetChatClient(modelIdChat).AsIChatClient();

Console.WriteLine($"\n--- {modelIdChat} response (no memory) ---");
await foreach (var update in chatClient.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, question)]))
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
    }
}

Console.WriteLine("\n\n--- Importing facts with local embeddings ---");
string[] facts =
[
    ".NET local embeddings can run offline using ONNX Runtime and HuggingFace sentence transformer models.",
    "LocalEmbeddingGenerator implements IEmbeddingGenerator<string, Embedding<float>> from Microsoft.Extensions.AI.",
    "The default model in LocalEmbeddings is sentence-transformers/all-MiniLM-L6-v2 with 384 dimensions.",
    "EmbeddingExtensions.CosineSimilarity compares vectors by angle, which is useful for semantic search.",
    "EmbeddingExtensions.FindClosest returns the top K items sorted by descending cosine similarity score.",
    "Set LocalEmbeddingsOptions.ModelPath to load a local ONNX model directory instead of downloading."
];

using var embeddingGenerator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());
var factEmbeddings = await embeddingGenerator.GenerateAsync(facts);
var indexedFacts = facts.Zip(factEmbeddings, (fact, embedding) => (Item: fact, Embedding: embedding));
var queryEmbeddings = await embeddingGenerator.GenerateAsync([question]);
if (queryEmbeddings.Count == 0)
{
    Console.WriteLine("Error: Failed to generate query embedding. Verify the local embedding model is available.");
    return;
}

var contextDocs = indexedFacts
    .FindClosest(queryEmbeddings[0], topK: topK)
    .Select(match => match.Item);

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
