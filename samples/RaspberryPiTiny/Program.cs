using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;

var mode = ParseMode(args);

Console.WriteLine("LocalEmbeddings Raspberry Pi Tiny Sample");
Console.WriteLine($"Mode: {mode}");

var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    EnsureModelDownloaded = true,
    MaxSequenceLength = 64,
    UseParallelExecution = false,
    InterOpNumThreads = 1,
    IntraOpNumThreads = 1
};

var started = DateTime.UtcNow;
using var generator = new LocalEmbeddingGenerator(options);
var loadElapsed = DateTime.UtcNow - started;

Console.WriteLine($"Model initialized in {loadElapsed.TotalSeconds:F2}s");

if (mode == "hello")
{
    await RunHelloAsync(generator);
}
else
{
    await RunSimilarityAsync(generator);
}

Console.WriteLine("Done.");

static string ParseMode(string[] inputArgs)
{
    if (inputArgs.Length == 0)
    {
        return "hello";
    }

    var value = inputArgs[0].Trim().ToLowerInvariant();
    return value is "sim" or "similarity" ? "sim" : "hello";
}

static async Task RunHelloAsync(LocalEmbeddingGenerator generator)
{
    const string text = "Hello from Raspberry Pi.";
    var embedding = await generator.GenerateEmbeddingAsync(text);

    Console.WriteLine($"Text: {text}");
    Console.WriteLine($"Dimensions: {embedding.Vector.Length}");
    Console.WriteLine($"First 3 values: [{string.Join(", ", embedding.Vector.ToArray().Take(3).Select(v => v.ToString("F6")))}]");
}

static async Task RunSimilarityAsync(LocalEmbeddingGenerator generator)
{
    const string textA = "I like coding in C#.";
    const string textB = "I enjoy developing .NET apps.";

    var embeddings = await generator.GenerateAsync([textA, textB]);
    var similarity = embeddings[0].CosineSimilarity(embeddings[1]);

    Console.WriteLine($"A: {textA}");
    Console.WriteLine($"B: {textB}");
    Console.WriteLine($"Cosine similarity: {similarity:F4}");
}
