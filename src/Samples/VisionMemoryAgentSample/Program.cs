using System.ComponentModel;
using System.Numerics.Tensors;
using ElBruno.LocalEmbeddings.ImageEmbeddings;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

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
    [Description("Absolute or relative path to the image file")] string path)
{
    if (!File.Exists(path))
        return $"Error: file not found: {path}";

    float[] embedding = imageEncoder.Encode(path);
    imageStore.Add((path, string.Empty, embedding));
    return $"Ingested '{Path.GetFileName(path)}'. Store now has {imageStore.Count} image(s).";
}

[Description("Ingest all the images from a local folder and store its CLIP embedding in memory. Returns confirmation.")]
string IngestImagesFromFolder(
    [Description("Absolute or relative path to the folder")] string folderPath)
{
    if (!Directory.Exists(folderPath))
        return $"Error: folder not found: {folderPath}";

    string[] extensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
    var imageFiles = Directory.GetFiles(folderPath)
        .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .ToList();

    if (imageFiles.Count == 0)
        return $"No images found in folder: {folderPath}";

    int ingestedCount = 0;
    foreach (string imagePath in imageFiles)
    {
        float[] embedding = imageEncoder.Encode(imagePath);
        imageStore.Add((imagePath, string.Empty, embedding));
        ingestedCount++;
    }

    return $"Ingested {ingestedCount} image(s) from '{folderPath}'. Store now has {imageStore.Count} image(s).";
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
        $"  {i + 1}. {Path.GetFileName(r.Path)} (score: {r.Score:F4})");

    return $"Top {results.Count} result(s):\n{string.Join("\n", lines)}";
}

// =============================================================================
// Create Ollama chat client with tool calling
// =============================================================================
Console.WriteLine($"Connecting to Ollama ({ollamaModel})...");

IChatClient chatClient = new OllamaChatClient(new Uri("http://localhost:11434"), modelId: ollamaModel)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var tools = new[]
{
    AIFunctionFactory.Create(IngestImage),
    AIFunctionFactory.Create(FindSimilarImages),
    AIFunctionFactory.Create(IngestImagesFromFolder)
};

AIAgent agent = chatClient.AsAIAgent(
    name: "VisionMemoryAgent",
    instructions: """
        You are a Vision Memory agent. You help users manage and search a local image collection.
        You have two tools:
        - IngestImage: to add an image to the in-memory store
        - IngestImagesFromFolder: to add all images from a folder to the in-memory store
        - FindSimilarImages: to search stored images using natural language
        Always use the tools when the user asks to ingest or search images.
        Report tool results clearly.
        """,
    tools: [.. tools]);

AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine();
Console.WriteLine("Vision Memory Agent ready! Type a message (or 'exit' to quit).");
Console.WriteLine("Examples:");
Console.WriteLine("  > Please ingest the image at ./samples/images/cat.jpg");
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

    ChatMessage message = new(ChatRole.User, input);
    AgentResponse response = await agent.RunAsync([message], session);

    Console.WriteLine();
    Console.WriteLine(response.Text);
    Console.WriteLine();

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
