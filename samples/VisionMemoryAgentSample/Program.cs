using System.ComponentModel;
using System.Numerics.Tensors;
using ElBruno.LocalEmbeddings.ImageEmbeddings;
using Microsoft.Extensions.AI;
using OllamaSharp;

// =============================================================================
// Parse arguments
// =============================================================================
string modelDir = GetArg(args, "--model-dir", "-m") ?? "";
string ollamaModel = GetArg(args, "--ollama-model", "-o") ?? "llama3.2";

if (string.IsNullOrEmpty(modelDir))
{
    Console.WriteLine("Usage: VisionMemoryAgentSample --model-dir <clip-model-dir> [--ollama-model <model>]");
    Console.WriteLine();
    Console.WriteLine("  --model-dir, -m    Directory with CLIP ONNX models (text_model.onnx, vision_model.onnx, vocab.json, merges.txt)");
    Console.WriteLine("  --ollama-model, -o Ollama model name (default: llama3.2)");
    return;
}

// =============================================================================
// Initialize CLIP encoders
// =============================================================================
Console.WriteLine("Loading CLIP models...");
using var textEncoder = new ClipTextEncoder(
    Path.Combine(modelDir, "text_model.onnx"),
    Path.Combine(modelDir, "vocab.json"),
    Path.Combine(modelDir, "merges.txt"));
using var imageEncoder = new ClipImageEncoder(
    Path.Combine(modelDir, "vision_model.onnx"));
Console.WriteLine("CLIP models loaded.");

// =============================================================================
// In-memory image store
// =============================================================================
var imageStore = new List<(string Path, string Tags, float[] Embedding)>();

// =============================================================================
// Tool definitions
// =============================================================================
[Description("Ingest a local image file and store its CLIP embedding in memory. Returns confirmation.")]
string IngestImage(
    [Description("Absolute or relative path to the image file")] string path,
    [Description("Comma-separated tags describing the image")] string tagsCsv)
{
    if (!File.Exists(path))
        return $"Error: file not found: {path}";

    float[] embedding = imageEncoder.Encode(path);
    imageStore.Add((path, tagsCsv, embedding));
    return $"Ingested '{Path.GetFileName(path)}' with tags [{tagsCsv}]. Store now has {imageStore.Count} image(s).";
}

[Description("Find images similar to a natural language query using CLIP embeddings. Returns top matches.")]
string FindSimilarImages(
    [Description("Natural language search query")] string query,
    [Description("Number of top results to return")] int topK = 3)
{
    if (imageStore.Count == 0)
        return "No images in store. Ingest some images first.";

    float[] queryEmbedding = textEncoder.Encode(query);

    var results = imageStore
        .Select(img => (img.Path, img.Tags, Score: TensorPrimitives.CosineSimilarity(
            queryEmbedding.AsSpan(), img.Embedding.AsSpan())))
        .OrderByDescending(r => r.Score)
        .Take(topK)
        .ToList();

    var lines = results.Select((r, i) =>
        $"  {i + 1}. {Path.GetFileName(r.Path)} (score: {r.Score:F4}, tags: {r.Tags})");

    return $"Top {results.Count} result(s):\n{string.Join("\n", lines)}";
}

// =============================================================================
// Create Ollama chat client with tool calling
// =============================================================================
Console.WriteLine($"Connecting to Ollama ({ollamaModel})...");

IChatClient ollamaClient = new OllamaApiClient(new Uri("http://localhost:11434"), ollamaModel);
IChatClient client = ollamaClient.AsBuilder()
    .UseFunctionInvocation()
    .Build();

var tools = new[]
{
    AIFunctionFactory.Create(IngestImage),
    AIFunctionFactory.Create(FindSimilarImages)
};

var chatOptions = new ChatOptions { Tools = [.. tools] };
var chatHistory = new List<ChatMessage>
{
    new(ChatRole.System,
        """
        You are a Vision Memory agent. You help users manage and search a local image collection.
        You have two tools:
        - IngestImage: to add an image to the in-memory store
        - FindSimilarImages: to search stored images using natural language
        Always use the tools when the user asks to ingest or search images.
        Report tool results clearly.
        """)
};

Console.WriteLine();
Console.WriteLine("Vision Memory Agent ready! Type a message (or 'exit' to quit).");
Console.WriteLine("Examples:");
Console.WriteLine("  > Please ingest the image at ./photos/cat.jpg with tags cat,pet,animal");
Console.WriteLine("  > Find images similar to 'a sunset over the ocean'");
Console.WriteLine();

// =============================================================================
// Chat loop
// =============================================================================
while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    chatHistory.Add(new ChatMessage(ChatRole.User, input));

    ChatResponse response = await client.GetResponseAsync(chatHistory, chatOptions);

    Console.WriteLine();
    Console.WriteLine(response.Text);
    Console.WriteLine();

    chatHistory.AddRange(response.Messages);
}

Console.WriteLine("Goodbye!");

// =============================================================================
// Helpers
// =============================================================================
static string? GetArg(string[] args, string longName, string shortName)
{
    for (int i = 0; i < args.Length; i++)
    {
        if ((args[i].Equals(longName, StringComparison.OrdinalIgnoreCase) ||
             args[i].Equals(shortName, StringComparison.OrdinalIgnoreCase)) &&
            i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }
    return null;
}
