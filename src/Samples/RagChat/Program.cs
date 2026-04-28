using ElBruno.LocalEmbeddings.VectorData.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using RagChat.ConsoleUi;
using RagChat.Data;
using RagChat.Helpers;
using RagChat.Models;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

const int TopMatches = 3;
const double MinimumScore = 0.2d;

RagChatConsoleRenderer.PrintBanner();

// =============================================================================
// Step 1: Configure Dependency Injection
// =============================================================================
RagChatConsoleRenderer.PrintStepHeader("Step 1: Setting up services with Dependency Injection");
RagChatConsoleRenderer.PrintInfo("→ Configuring ServiceCollection with AddLocalEmbeddingsWithInMemoryVectorStore(...)");

var services = new ServiceCollection();

services.AddLocalEmbeddingsWithInMemoryVectorStore(options =>
{
    // Change this model name to try different embedding models from Hugging Face.
    // Model cards:
    // - sentence-transformers/all-MiniLM-L6-v2      https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2
    // - sentence-transformers/all-MiniLM-L12-v2     https://huggingface.co/sentence-transformers/all-MiniLM-L12-v2
    // - sentence-transformers/paraphrase-MiniLM-L6-v2 https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2
    // - BAAI/bge-large-en-v1.5                      https://huggingface.co/BAAI/bge-large-en-v1.5
    // - intfloat/e5-large-v2                        https://huggingface.co/intfloat/e5-large-v2
    //
    // Example quick switches:
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";

    // options.ModelName = "sentence-transformers/all-MiniLM-L12-v2";
    // options.ModelName = "sentence-transformers/paraphrase-MiniLM-L6-v2";
    // options.ModelName = "BAAI/bge-large-en-v1.5";
    // options.ModelName = "intfloat/e5-large-v2";

    options.MaxSequenceLength = 256;
    options.EnsureModelDownloaded = true;
})
.AddVectorStoreCollection<string, Document>("faq");

RagChatConsoleRenderer.PrintInfo("→ Building service provider");
using var serviceProvider = services.BuildServiceProvider();
RagChatConsoleRenderer.PrintSuccess("Services configured successfully");
AnsiConsole.WriteLine();

// =============================================================================
// Step 2: Initialize Components
// =============================================================================
RagChatConsoleRenderer.PrintStepHeader("Step 2: Initializing Embedding Generator and Vector Store");
RagChatConsoleRenderer.PrintInfo("→ Resolving IEmbeddingGenerator from DI container");

var startTime = DateTime.Now;
var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
var loadTime = DateTime.Now - startTime;

RagChatConsoleRenderer.PrintSuccess($"Embedding generator ready ({loadTime.TotalSeconds:F2}s)");

if (embeddingGenerator is ElBruno.LocalEmbeddings.LocalEmbeddingGenerator localGen)
{
    RagChatConsoleRenderer.PrintInfo($"• Provider: {localGen.Metadata.ProviderName}");
    RagChatConsoleRenderer.PrintInfo($"• Model: {localGen.Metadata.DefaultModelId}");
    RagChatConsoleRenderer.PrintInfo($"• Dimensions: {localGen.Metadata.DefaultModelDimensions}");
}

AnsiConsole.WriteLine();
RagChatConsoleRenderer.PrintInfo("→ Resolving VectorData collection (faq)");
var faqCollection = serviceProvider.GetRequiredService<VectorStoreCollection<string, Document>>();
RagChatConsoleRenderer.PrintSuccess("Shared InMemoryVectorStore collection initialized");
AnsiConsole.WriteLine();

// =============================================================================
// Step 3: Load Sample Data
// =============================================================================
RagChatConsoleRenderer.PrintStepHeader("Step 3: Loading Knowledge Base");

var documents = SampleData.GetFaqDocuments();
RagChatConsoleRenderer.PrintInfo($"→ Loading {documents.Count} FAQ documents...");
AnsiConsole.WriteLine();

var categories = documents.GroupBy(d => d.Category).ToList();
RagChatConsoleRenderer.PrintInfo("Document Categories:");
foreach (var category in categories)
{
    RagChatConsoleRenderer.PrintInfo($"  • {category.Key}: {category.Count()} documents");
}

AnsiConsole.WriteLine();
RagChatConsoleRenderer.PrintInfo("→ Generating embeddings for all documents...");

startTime = DateTime.Now;
var contents = documents.Select(d => d.Content).ToList();
var embeddings = await embeddingGenerator.GenerateAsync(contents);

await AnsiConsole.Progress()
    .AutoRefresh(true)
    .AutoClear(true)
    .Columns(
    [
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn(),
        new SpinnerColumn(),
    ])
    .StartAsync(async context =>
    {
        var task = context.AddTask("[green]Indexing FAQ documents[/]", maxValue: documents.Count);
        for (var i = 0; i < documents.Count; i++)
        {
            documents[i].Vector = embeddings[i].Vector;
            task.Increment(1);
            await Task.Yield();
        }
    });

await faqCollection.UpsertAsync(documents);

var embeddingTime = DateTime.Now - startTime;
RagChatConsoleRenderer.PrintSuccess($"Generated {documents.Count} embeddings in {embeddingTime.TotalSeconds:F2}s");
RagChatConsoleRenderer.PrintInfo($"Average: {embeddingTime.TotalMilliseconds / documents.Count:F1}ms per document");
AnsiConsole.WriteLine();

// =============================================================================
// Step 4: Interactive Q&A Loop
// =============================================================================
RagChatConsoleRenderer.PrintStepHeader("Step 4: Interactive Q&A");
RagChatConsoleRenderer.PrintStartupInstructions();
RagChatConsoleRenderer.PrintChatStarted();

while (true)
{
    var input = RagChatConsoleRenderer.ReadUserInput();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.WriteLine();
        RagChatConsoleRenderer.PrintGoodbye();
        break;
    }

    if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
    {
        RagChatConsoleRenderer.PrintHelp();
        continue;
    }

    if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
    {
        RagChatConsoleRenderer.PrintDocumentList(documents);
        continue;
    }

    AnsiConsole.WriteLine();

    startTime = DateTime.Now;
    var queryEmbedding = (await embeddingGenerator.GenerateAsync([input]))[0];
    var rawResults = await AsyncEnumerableHelpers.ToListAsync(faqCollection.SearchAsync(queryEmbedding, top: TopMatches));
    var results = rawResults
        .Where(r => (r.Score ?? 0d) >= MinimumScore)
        .OrderByDescending(r => r.Score ?? 0d)
        .ToList();
    var searchTime = DateTime.Now - startTime;

    if (results.Count == 0)
    {
        RagChatConsoleRenderer.PrintNoResults();
    }
    else
    {
        RagChatConsoleRenderer.PrintResults(results, searchTime);
    }

    RagChatConsoleRenderer.PrintDivider();
}

AnsiConsole.WriteLine();
RagChatConsoleRenderer.PrintSessionComplete();
