using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         LocalEmbeddings Sample Console Application            ║");
Console.WriteLine("║     Generate embeddings locally using ONNX Runtime            ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// EXAMPLE 1: Basic usage with default model (all-MiniLM-L6-v2)
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 1: Basic Usage with Default Model");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

Console.WriteLine("Creating embedding generator with default settings...");
Console.WriteLine("Model: sentence-transformers/all-MiniLM-L6-v2");
Console.WriteLine();

// Show download progress when model downloads
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    EnsureModelDownloaded = true
};

Console.WriteLine("Initializing model (will download if not cached)...");
var startTime = DateTime.Now;

using var generator = new LocalEmbeddingGenerator(options);

var loadTime = DateTime.Now - startTime;
Console.WriteLine($"✓ Model loaded in {loadTime.TotalSeconds:F2} seconds");
Console.WriteLine($"  Provider: {generator.Metadata.ProviderName}");
Console.WriteLine($"  Model: {generator.Metadata.DefaultModelId}");
Console.WriteLine($"  Embedding Dimensions: {generator.Metadata.DefaultModelDimensions}");
Console.WriteLine();

// =============================================================================
// EXAMPLE 2: Generating embeddings for a single string
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 2: Generate Embedding for a Single String");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var singleText = "The quick brown fox jumps over the lazy dog.";
Console.WriteLine($"Input text: \"{singleText}\"");
Console.WriteLine();

// Single-string overload — no array wrapping needed!
var singleEmbedding = await generator.GenerateAsync(singleText);
var vector = singleEmbedding[0].Vector;

Console.WriteLine($"✓ Generated embedding with {vector.Length} dimensions");
Console.WriteLine($"  First 5 values: [{string.Join(", ", vector.ToArray().Take(5).Select(v => v.ToString("F6")))}...]");
Console.WriteLine($"  Vector norm: {Math.Sqrt(vector.ToArray().Sum(v => v * v)):F6}");
Console.WriteLine();

// =============================================================================
// EXAMPLE 3: Generating embeddings for multiple strings (batch)
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 3: Batch Embedding Generation");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var documents = new List<string>
{
    "Machine learning is a subset of artificial intelligence.",
    "Deep learning uses neural networks with many layers.",
    "Natural language processing helps computers understand text.",
    "Computer vision enables machines to interpret images.",
    "Reinforcement learning trains agents through rewards."
};

Console.WriteLine("Input documents:");
for (var i = 0; i < documents.Count; i++)
{
    Console.WriteLine($"  [{i}] {documents[i]}");
}
Console.WriteLine();

startTime = DateTime.Now;
var batchEmbeddings = await generator.GenerateAsync(documents);
var batchTime = DateTime.Now - startTime;

Console.WriteLine($"✓ Generated {batchEmbeddings.Count} embeddings in {batchTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"  Average time per document: {batchTime.TotalMilliseconds / documents.Count:F2}ms");
Console.WriteLine();

// =============================================================================
// EXAMPLE 4: Using cosine similarity to compare embeddings
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 4: Semantic Similarity with Cosine Similarity");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

// Define pairs of sentences to compare
var sentencePairs = new (string, string)[]
{
    ("I love programming", "I enjoy coding"),  // Similar
    ("I love programming", "The weather is nice today"),  // Different
    ("Machine learning is fascinating", "AI and ML are interesting fields"),  // Similar
    ("The cat sat on the mat", "A feline rested on the rug")  // Similar (paraphrase)
};

Console.WriteLine("Comparing sentence pairs:");
Console.WriteLine();

foreach (var (sentence1, sentence2) in sentencePairs)
{
    var embeddings = await generator.GenerateAsync([sentence1, sentence2]);
    var similarity = embeddings[0].CosineSimilarity(embeddings[1]);

    var similarityBar = new string('█', (int)(similarity * 20));
    var emptyBar = new string('░', 20 - (int)(similarity * 20));

    Console.WriteLine($"  \"{sentence1}\"");
    Console.WriteLine($"  \"{sentence2}\"");
    Console.WriteLine($"  Similarity: [{similarityBar}{emptyBar}] {similarity:P1}");
    Console.WriteLine();
}

Console.WriteLine("All-pairs similarity matrix (SentenceTransformers-style):");
var matrixSentences = new[]
{
    "The weather is lovely today.",
    "It's so sunny outside!",
    "He drove to the stadium."
};

var matrixEmbeddings = await generator.GenerateAsync(matrixSentences);
var similarityMatrix = matrixEmbeddings.Similarity();

for (var i = 0; i < matrixSentences.Length; i++)
{
    Console.Write("  ");
    for (var j = 0; j < matrixSentences.Length; j++)
    {
        Console.Write($"{similarityMatrix[i, j],8:F4}");
    }

    Console.WriteLine();
}
Console.WriteLine();

// =============================================================================
// EXAMPLE 5: Practical Use Case - Semantic Search
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 5: Practical Use Case - Semantic Search");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

// Create a simple knowledge base
var knowledgeBase = new List<string>
{
    "Python is a popular programming language for data science and machine learning.",
    "JavaScript is widely used for web development and runs in browsers.",
    "C# is developed by Microsoft and commonly used for Windows applications.",
    "Rust focuses on memory safety and is great for systems programming.",
    "Go was created by Google and excels at concurrent programming.",
    "TypeScript adds static typing to JavaScript for better code quality.",
    "Java is platform-independent and runs on the JVM.",
    "Swift is Apple's language for iOS and macOS development."
};

Console.WriteLine("Knowledge Base:");
for (var i = 0; i < knowledgeBase.Count; i++)
{
    Console.WriteLine($"  [{i}] {knowledgeBase[i]}");
}
Console.WriteLine();

// Pre-compute embeddings for the knowledge base
Console.WriteLine("Computing embeddings for knowledge base...");
var kbEmbeddings = await generator.GenerateAsync(knowledgeBase);
Console.WriteLine($"✓ Indexed {kbEmbeddings.Count} documents");
Console.WriteLine();

// Perform semantic searches
var queries = new[]
{
    "What language should I use for building websites?",
    "I want to build an iPhone app",
    "Which language is best for AI projects?"
};

foreach (var query in queries)
{
    Console.WriteLine($"Query: \"{query}\"");
    Console.WriteLine();

    // Single-string GenerateEmbeddingAsync — returns the embedding directly
    var queryEmbedding = await generator.GenerateEmbeddingAsync(query);

    // Calculate similarity with all documents
    var results = knowledgeBase
        .Select((doc, idx) => new
        {
            Document = doc,
            Similarity = queryEmbedding.CosineSimilarity(kbEmbeddings[idx])
        })
        .OrderByDescending(r => r.Similarity)
        .Take(3)
        .ToList();

    Console.WriteLine("  Top 3 Results:");
    for (var i = 0; i < results.Count; i++)
    {
        var bar = new string('█', (int)(results[i].Similarity * 15));
        var empty = new string('░', 15 - (int)(results[i].Similarity * 15));
        Console.WriteLine($"    {i + 1}. [{bar}{empty}] {results[i].Similarity:P1}");
        Console.WriteLine($"       {results[i].Document}");
    }
    Console.WriteLine();
}

// =============================================================================
// EXAMPLE 6: Using Dependency Injection with AddLocalEmbeddings()
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 6: Dependency Injection with AddLocalEmbeddings()");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

Console.WriteLine("Setting up dependency injection container...");
Console.WriteLine();

var services = new ServiceCollection();

// Register LocalEmbeddings with custom options
services.AddLocalEmbeddings(opts =>
{
    opts.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    opts.MaxSequenceLength = 256;
});

var serviceProvider = services.BuildServiceProvider();

// Resolve the embedding generator from DI
using var scope = serviceProvider.CreateScope();
var diGenerator = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

Console.WriteLine("✓ IEmbeddingGenerator resolved from DI container");

// Cast to LocalEmbeddingGenerator to access Metadata (or use pattern matching)
if (diGenerator is LocalEmbeddingGenerator localGenerator)
{
    Console.WriteLine($"  Provider: {localGenerator.Metadata.ProviderName}");
    Console.WriteLine($"  Model: {localGenerator.Metadata.DefaultModelId}");
}
Console.WriteLine();

// Use the DI-injected generator
var diTestText = "Testing dependency injection with LocalEmbeddings!";
// Works through DI — the extension method is on IEmbeddingGenerator<string, Embedding<float>>
var diEmbedding = await diGenerator.GenerateEmbeddingAsync(diTestText);

Console.WriteLine($"Generated embedding for: \"{diTestText}\"");
Console.WriteLine($"  Dimensions: {diEmbedding.Vector.Length}");
Console.WriteLine();

// =============================================================================
// Summary
// =============================================================================
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                      Sample Complete!                          ║");
Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  LocalEmbeddings provides:                                     ║");
Console.WriteLine("║  • Local embedding generation (no API calls needed)            ║");
Console.WriteLine("║  • Microsoft.Extensions.AI compatible interface                ║");
Console.WriteLine("║  • Automatic model downloading and caching                     ║");
Console.WriteLine("║  • Batch processing for efficient embedding generation         ║");
Console.WriteLine("║  • Easy DI integration with AddLocalEmbeddings()               ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");

return;
