using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         Zero-Cloud RAG Foundation Sample                       ║");
Console.WriteLine("║   Semantic search with local embeddings - No cloud needed     ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

Console.WriteLine("This sample demonstrates the foundation of a RAG system:");
Console.WriteLine("  1. Local embedding generation");
Console.WriteLine("  2. Document knowledge base");
Console.WriteLine("  3. Semantic search and retrieval");
Console.WriteLine();
Console.WriteLine("NOTE: For a complete RAG sample with local LLM integration,");
Console.WriteLine("      see the LocalLlmRag sample.");
Console.WriteLine();

// =============================================================================
// Step 1: Initialize local embedding generator
// =============================================================================
Console.WriteLine("Step 1: Initializing local embedding generator...");
Console.WriteLine("  Model: sentence-transformers/all-MiniLM-L6-v2");
Console.WriteLine("  (Model will auto-download on first run)");
Console.WriteLine();

var embeddingOptions = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    EnsureModelDownloaded = true
};

var startTime = DateTime.Now;
var embedder = await LocalEmbeddingGenerator.CreateAsync(embeddingOptions);
var loadTime = DateTime.Now - startTime;

Console.WriteLine($"✓ Embedding generator ready in {loadTime.TotalSeconds:F2} seconds");
Console.WriteLine($"  Embedding dimensions: {embedder.Metadata.DefaultModelDimensions}");
Console.WriteLine();

// =============================================================================
// Step 2: Create sample documents about .NET topics
// =============================================================================
Console.WriteLine("Step 2: Creating sample knowledge base...");
Console.WriteLine();

var documents = new List<string>
{
    ".NET is a free, open-source development platform created by Microsoft for building many different types of applications. With .NET, you can use multiple languages, editors, and libraries to build for web, mobile, desktop, games, IoT, and more.",
    
    "C# is a modern, object-oriented programming language developed by Microsoft. It runs on the .NET platform and is used for building Windows applications, web services, and cloud-based applications. C# supports features like LINQ, async/await, and pattern matching.",
    
    "ASP.NET Core is a cross-platform, high-performance framework for building modern, cloud-enabled, internet-connected applications. You can use ASP.NET Core to build web apps, web APIs, microservices, and real-time applications using SignalR.",
    
    "Blazor is a framework for building interactive web UIs using C# instead of JavaScript. It allows developers to create rich web applications with .NET and C# on the client side using WebAssembly or on the server side with SignalR.",
    
    "Entity Framework Core is a modern object-database mapper for .NET. It supports LINQ queries, change tracking, updates, and schema migrations. EF Core works with SQL Server, Azure SQL Database, SQLite, PostgreSQL, MySQL, and many other databases.",
    
    "MAUI (Multi-platform App UI) is a cross-platform framework for creating native mobile and desktop apps with C# and XAML. With a single codebase, you can build apps for Android, iOS, macOS, and Windows.",
    
    "NuGet is the package manager for .NET. It enables developers to share and consume useful code through a centralized package repository. NuGet packages contain compiled code (DLLs) along with other content and a manifest file.",
    
    "LINQ (Language Integrated Query) is a powerful feature in C# that provides query capabilities directly in the C# language. You can use LINQ to query arrays, collections, XML documents, databases, and other data sources with a consistent syntax.",
    
    "Minimal APIs in ASP.NET Core allow you to build HTTP APIs with minimal dependencies and configuration. They're ideal for microservices and apps that want to include only the minimum files, features, and dependencies in ASP.NET Core.",
    
    "Dependency Injection is a design pattern built into .NET Core that helps create loosely coupled, testable code. The framework provides a built-in service container (IServiceProvider) for registering and resolving dependencies."
};

Console.WriteLine($"Created knowledge base with {documents.Count} documents:");
for (var i = 0; i < Math.Min(3, documents.Count); i++)
{
    var preview = documents[i].Length > 80 ? documents[i][..77] + "..." : documents[i];
    Console.WriteLine($"  [{i + 1}] {preview}");
}
Console.WriteLine($"  ... and {documents.Count - 3} more documents");
Console.WriteLine();

// =============================================================================
// Step 3: Generate embeddings for all documents
// =============================================================================
Console.WriteLine("Step 3: Embedding all documents...");

startTime = DateTime.Now;
var documentEmbeddings = await embedder.GenerateAsync(documents);
var embeddingTime = DateTime.Now - startTime;

Console.WriteLine($"✓ Generated {documentEmbeddings.Count} embeddings in {embeddingTime.TotalSeconds:F2} seconds");
Console.WriteLine($"  Average: {embeddingTime.TotalMilliseconds / documents.Count:F1}ms per document");
Console.WriteLine();

// =============================================================================
// Step 4: Perform semantic search queries
// =============================================================================
Console.WriteLine("Step 4: Demonstrating semantic search...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var queries = new[]
{
    "How do I build web applications with .NET?",
    "What is LINQ and how is it used?",
    "Tell me about mobile app development in .NET",
    "How does dependency injection work?"
};

foreach (var query in queries)
{
    Console.WriteLine($"Query: \"{query}\"");
    Console.WriteLine();

    // Retrieve top-K relevant documents
    var topK = 3;
    var results = await embedder.FindClosestAsync(query, documents, documentEmbeddings, topK: topK, minScore: 0.2f);

    Console.WriteLine($"  Retrieved {results.Count} relevant documents:");
    for (var i = 0; i < results.Count; i++)
    {
        var scoreBar = new string('█', (int)(results[i].Score * 15));
        var emptyBar = new string('░', 15 - (int)(results[i].Score * 15));
        var preview = results[i].Text.Length > 80 ? results[i].Text[..77] + "..." : results[i].Text;
        Console.WriteLine($"    [{i + 1}] [{scoreBar}{emptyBar}] {results[i].Score:F4}");
        Console.WriteLine($"        {preview}");
    }
    
    Console.WriteLine();
    Console.WriteLine("  → In a complete RAG system, these documents would be sent to an LLM");
    Console.WriteLine("     for answer generation. See the LocalLlmRag sample for that.");
    Console.WriteLine();
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine();
}

// =============================================================================
// Step 5: Show similarity comparison between documents
// =============================================================================
Console.WriteLine("Step 5: Document similarity matrix...");
Console.WriteLine();

Console.WriteLine("Comparing first 5 documents with each other:");
var firstFiveDocs = documents.Take(5).ToList();
var firstFiveEmbeddings = documentEmbeddings.Take(5).ToList();

for (var i = 0; i < firstFiveDocs.Count; i++)
{
    for (var j = i + 1; j < firstFiveDocs.Count; j++)
    {
        var similarity = firstFiveEmbeddings[i].CosineSimilarity(firstFiveEmbeddings[j]);
        var bar = new string('█', (int)(similarity * 20));
        var empty = new string('░', 20 - (int)(similarity * 20));
        
        var doc1Preview = firstFiveDocs[i].Length > 40 ? firstFiveDocs[i][..37] + "..." : firstFiveDocs[i];
        var doc2Preview = firstFiveDocs[j].Length > 40 ? firstFiveDocs[j][..37] + "..." : firstFiveDocs[j];
        
        Console.WriteLine($"  [{i}] vs [{j}]: [{bar}{empty}] {similarity:F4}");
        Console.WriteLine($"      {doc1Preview}");
        Console.WriteLine($"      {doc2Preview}");
        Console.WriteLine();
    }
}

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║           Zero-Cloud RAG Foundation Complete!                  ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  This sample demonstrated:                                     ║");
Console.WriteLine("║  ✓ Local embedding generation (no API calls)                   ║");
Console.WriteLine("║  ✓ Semantic search with FindClosestAsync                       ║");
Console.WriteLine("║  ✓ Document similarity comparison                              ║");
Console.WriteLine("║  ✓ RAG retrieval pipeline (without LLM)                        ║");
Console.WriteLine("║  ✓ 100% offline - all processing done locally                  ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  Next step: See LocalLlmRag sample for LLM integration!        ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
