using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                   LocalEmbeddings Lite Sample                ║");
Console.WriteLine("║                Raspberry Pi / low-resource friendly          ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

Console.WriteLine("This sample runs one small scenario and exits.");
Console.WriteLine("Use an argument to skip the menu: 1 or 2");
Console.WriteLine();

var selectedScenario = ParseScenario(args);

if (selectedScenario is null)
{
    selectedScenario = PromptScenario();
}

if (selectedScenario is null)
{
    Console.WriteLine("No valid scenario selected. Exiting.");
    return;
}

var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    EnsureModelDownloaded = true,
    MaxSequenceLength = 128,
    UseParallelExecution = false,
    InterOpNumThreads = 1,
    IntraOpNumThreads = 1
};

Console.WriteLine($"Initializing model: {options.ModelName}");
var start = DateTime.UtcNow;

using var generator = new LocalEmbeddingGenerator(options);

var loadTime = DateTime.UtcNow - start;
Console.WriteLine($"Model ready in {loadTime.TotalSeconds:F2}s");
Console.WriteLine();

switch (selectedScenario)
{
    case 1:
        await RunHelloWorldScenarioAsync(generator);
        break;
    case 2:
        await RunSimilarityScenarioAsync(generator);
        break;
}

Console.WriteLine();
Console.WriteLine("Done ✅");

return;

static int? ParseScenario(string[] inputArgs)
{
    if (inputArgs.Length == 0)
    {
        return null;
    }

    return int.TryParse(inputArgs[0], out var parsed) && (parsed == 1 || parsed == 2)
        ? parsed
        : null;
}

static int? PromptScenario()
{
    Console.WriteLine("Select a scenario:");
    Console.WriteLine("  1) Hello World embedding");
    Console.WriteLine("  2) Two-text cosine similarity");
    Console.Write("Choice (1 or 2): ");

    var choice = Console.ReadLine();
    Console.WriteLine();

    return int.TryParse(choice, out var parsed) && (parsed == 1 || parsed == 2)
        ? parsed
        : null;
}

static async Task RunHelloWorldScenarioAsync(LocalEmbeddingGenerator generator)
{
    const string text = "Hello world from LocalEmbeddings Lite!";

    var embedding = await generator.GenerateEmbeddingAsync(text);

    Console.WriteLine("Scenario 1: Hello World embedding");
    Console.WriteLine($"Text: {text}");
    Console.WriteLine($"Dimensions: {embedding.Vector.Length}");
    Console.WriteLine($"First 5 values: [{string.Join(", ", embedding.Vector.ToArray().Take(5).Select(v => v.ToString("F6")))}...]");
}

static async Task RunSimilarityScenarioAsync(LocalEmbeddingGenerator generator)
{
    const string textA = "I enjoy building .NET applications.";
    const string textB = "I like creating apps with C# and .NET.";

    var embeddings = await generator.GenerateAsync([textA, textB]);
    var similarity = embeddings[0].CosineSimilarity(embeddings[1]);

    Console.WriteLine("Scenario 2: Cosine similarity");
    Console.WriteLine($"Text A: {textA}");
    Console.WriteLine($"Text B: {textB}");
    Console.WriteLine($"Cosine similarity: {similarity:F4} ({similarity:P1})");
}