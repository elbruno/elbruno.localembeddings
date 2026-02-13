using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using RagChat.Data;
using RagChat.VectorStore;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                     RAG Chat - Semantic Q&A Demo                              ║");
Console.WriteLine("║           Powered by LocalEmbeddings & Microsoft.Extensions.AI               ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure Dependency Injection
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Step 1: Setting up services with Dependency Injection");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

Console.WriteLine("  → Configuring ServiceCollection with AddLocalEmbeddings()");

var services = new ServiceCollection();

// Register LocalEmbeddings using the DI extension method
services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.MaxSequenceLength = 256;
    options.EnsureModelDownloaded = true;
});

// Register our vector store
services.AddSingleton<InMemoryVectorStore>();

Console.WriteLine("  → Building service provider");
using var serviceProvider = services.BuildServiceProvider();
Console.WriteLine("  ✓ Services configured successfully");
Console.WriteLine();

// =============================================================================
// Step 2: Initialize Components
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Step 2: Initializing Embedding Generator and Vector Store");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

Console.WriteLine("  → Resolving IEmbeddingGenerator from DI container");
var startTime = DateTime.Now;

var embeddingGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

var loadTime = DateTime.Now - startTime;
Console.WriteLine($"  ✓ Embedding generator ready ({loadTime.TotalSeconds:F2}s)");

// Display metadata
if (embeddingGenerator is ElBruno.LocalEmbeddings.LocalEmbeddingGenerator localGen)
{
    Console.WriteLine($"    • Provider: {localGen.Metadata.ProviderName}");
    Console.WriteLine($"    • Model: {localGen.Metadata.DefaultModelId}");
    Console.WriteLine($"    • Dimensions: {localGen.Metadata.DefaultModelDimensions}");
}
Console.WriteLine();

Console.WriteLine("  → Creating InMemoryVectorStore instance");
var vectorStore = serviceProvider.GetRequiredService<InMemoryVectorStore>();
Console.WriteLine("  ✓ Vector store initialized");
Console.WriteLine();

// =============================================================================
// Step 3: Load Sample Data
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Step 3: Loading Knowledge Base");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var documents = SampleData.GetFaqDocuments();
Console.WriteLine($"  → Loading {documents.Count} FAQ documents...");
Console.WriteLine();

// Show categories
var categories = documents.GroupBy(d => d.Category).ToList();
Console.WriteLine("  Document Categories:");
foreach (var category in categories)
{
    Console.WriteLine($"    • {category.Key}: {category.Count()} documents");
}
Console.WriteLine();

// Generate embeddings with progress
Console.WriteLine("  → Generating embeddings for all documents...");
Console.Write("    Progress: [");

startTime = DateTime.Now;
var totalDocs = documents.Count;
var progressWidth = 40;

await vectorStore.AddDocumentsAsync(documents, (current, total) =>
{
    var progress = (int)((float)current / total * progressWidth);
    Console.SetCursorPosition(15, Console.CursorTop);
    Console.Write("[" + new string('█', progress) + new string('░', progressWidth - progress) + $"] {current}/{total}");
});

var embeddingTime = DateTime.Now - startTime;
Console.WriteLine();
Console.WriteLine($"  ✓ Generated {documents.Count} embeddings in {embeddingTime.TotalSeconds:F2}s");
Console.WriteLine($"    Average: {embeddingTime.TotalMilliseconds / documents.Count:F1}ms per document");
Console.WriteLine();

// =============================================================================
// Step 4: Interactive Q&A Loop
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Step 4: Interactive Q&A");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();
Console.WriteLine("  Ask questions about LocalAI Assistant (the fictional product in our FAQ).");
Console.WriteLine("  Type 'quit' or 'exit' to end the session.");
Console.WriteLine("  Type 'help' to see example questions.");
Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                          Chat Session Started                                 ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You: ");
    Console.ResetColor();

    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    // Handle commands
    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine();
        Console.WriteLine("  Goodbye! Thanks for trying RAG Chat.");
        break;
    }

    if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
    {
        PrintHelp();
        continue;
    }

    if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
    {
        PrintDocumentList(documents);
        continue;
    }

    Console.WriteLine();

    // Perform semantic search
    startTime = DateTime.Now;
    var results = await vectorStore.SearchAsync(input, topK: 3, minScore: 0.2f);
    var searchTime = DateTime.Now - startTime;

    if (results.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  No relevant documents found. Try rephrasing your question.");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Found {results.Count} relevant document(s) in {searchTime.TotalMilliseconds:F0}ms:");
        Console.ResetColor();
        Console.WriteLine();

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var similarityPercent = result.Score * 100;
            var barLength = (int)(result.Score * 20);
            var bar = new string('█', barLength) + new string('░', 20 - barLength);

            // Color based on similarity score
            Console.ForegroundColor = result.Score >= 0.5f ? ConsoleColor.Green :
                                       result.Score >= 0.35f ? ConsoleColor.Yellow : ConsoleColor.DarkYellow;
            Console.Write($"  [{bar}] ");
            Console.ResetColor();
            Console.WriteLine($"{similarityPercent:F1}% match");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  📄 {result.Document.Title}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"     Category: {result.Document.Category}");
            Console.ResetColor();

            // Wrap content for better display
            var content = result.Document.Content;
            var maxWidth = 70;
            var lines = WrapText(content, maxWidth);
            foreach (var line in lines)
            {
                Console.WriteLine($"     {line}");
            }

            Console.WriteLine();
        }
    }

    Console.WriteLine("─────────────────────────────────────────────────────────────────────────────────");
    Console.WriteLine();
}

// =============================================================================
// Cleanup
// =============================================================================
Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                            Session Complete                                   ║");
Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  RAG Chat demonstrates:                                                       ║");
Console.WriteLine("║  • In-memory vector storage with embeddings                                   ║");
Console.WriteLine("║  • Semantic similarity search using cosine similarity                         ║");
Console.WriteLine("║  • Clean DI integration with AddLocalEmbeddings()                             ║");
Console.WriteLine("║  • Interactive chat-style Q&A interface                                       ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");

return;

// =============================================================================
// Helper Functions
// =============================================================================

static void PrintHelp()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────┐");
    Console.WriteLine("  │ Example Questions                                                       │");
    Console.WriteLine("  ├─────────────────────────────────────────────────────────────────────────┤");
    Console.WriteLine("  │ • What are the system requirements?                                    │");
    Console.WriteLine("  │ • How do I install the application?                                    │");
    Console.WriteLine("  │ • What features does the code assistant have?                          │");
    Console.WriteLine("  │ • Is my data private and secure?                                       │");
    Console.WriteLine("  │ • Why is the application running slowly?                               │");
    Console.WriteLine("  │ • What's the pricing for professional users?                           │");
    Console.WriteLine("  │ • How can I integrate with Visual Studio Code?                         │");
    Console.WriteLine("  │ • What should I do if the model won't load?                            │");
    Console.WriteLine("  ├─────────────────────────────────────────────────────────────────────────┤");
    Console.WriteLine("  │ Commands: 'list' = show all documents, 'quit'/'exit' = end session     │");
    Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────┘");
    Console.ResetColor();
    Console.WriteLine();
}

static void PrintDocumentList(List<Document> documents)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("  Knowledge Base Documents:");
    Console.ResetColor();
    Console.WriteLine();

    var grouped = documents.GroupBy(d => d.Category);
    foreach (var group in grouped)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  [{group.Key}]");
        Console.ResetColor();
        foreach (var doc in group)
        {
            Console.WriteLine($"    • {doc.Title}");
        }
        Console.WriteLine();
    }
}

static List<string> WrapText(string text, int maxWidth)
{
    var words = text.Split(' ');
    var lines = new List<string>();
    var currentLine = "";

    foreach (var word in words)
    {
        if (currentLine.Length + word.Length + 1 <= maxWidth)
        {
            currentLine += (currentLine.Length > 0 ? " " : "") + word;
        }
        else
        {
            if (currentLine.Length > 0)
                lines.Add(currentLine);
            currentLine = word;
        }
    }

    if (currentLine.Length > 0)
        lines.Add(currentLine);

    return lines;
}
