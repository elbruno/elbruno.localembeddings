using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.VectorData;
using ElBruno.LocalLLMs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║            Zero-Cloud RAG Sample Application                   ║");
Console.WriteLine("║   Complete offline RAG pipeline - No cloud services needed    ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Setup Dependency Injection with Local Embeddings and Local LLM
// =============================================================================
Console.WriteLine("Step 1: Setting up dependency injection...");
Console.WriteLine("  • Registering LocalEmbeddings (sentence-transformers/all-MiniLM-L6-v2)");
Console.WriteLine("  • Registering LocalLLMs (Phi-4)");
Console.WriteLine("  • Registering InMemoryVectorStore");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder();

// Register local embeddings with default model
builder.Services.AddLocalEmbeddings(opts =>
{
    opts.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    opts.EnsureModelDownloaded = true;
});

// Register local LLM (Phi-4)
builder.Services.AddLocalLLMs(opts =>
{
    opts.ModelId = "microsoft/phi-4";
    opts.EnsureModelDownloaded = true;
});

// Register InMemoryVectorStore for document storage
builder.Services.AddInMemoryVectorStore();

var host = builder.Build();

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
Console.WriteLine("Step 3: Generating embeddings for documents...");
Console.WriteLine("  (Models will auto-download on first run)");
Console.WriteLine();

var embedder = host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
var startTime = DateTime.Now;

// Generate embeddings for all documents in batch
var embeddings = await embedder.GenerateAsync(documents);

var embeddingTime = DateTime.Now - startTime;
Console.WriteLine($"✓ Generated {embeddings.Count} embeddings in {embeddingTime.TotalSeconds:F2} seconds");
Console.WriteLine($"  Average: {embeddingTime.TotalMilliseconds / documents.Count:F1}ms per document");
Console.WriteLine($"  Embedding dimensions: {embeddings[0].Vector.Length}");
Console.WriteLine();

// =============================================================================
// Step 4: Store embeddings in InMemoryVectorStore
// =============================================================================
Console.WriteLine("Step 4: Storing documents in vector store...");

var vectorStore = host.Services.GetRequiredService<IVectorStore>();
var collection = vectorStore.GetCollection<string, VectorStoreRecord<string>>("dotnet-docs");
await collection.CreateCollectionIfNotExistsAsync();

// Store each document with its embedding
for (var i = 0; i < documents.Count; i++)
{
    var record = new VectorStoreRecord<string>
    {
        Key = $"doc_{i}",
        Vector = embeddings[i].Vector.ToArray(),
        Data = new Dictionary<string, object?>
        {
            ["text"] = documents[i]
        }
    };
    await collection.UpsertAsync(record);
}

Console.WriteLine($"✓ Stored {documents.Count} documents in vector store");
Console.WriteLine();

// =============================================================================
// Step 5: Accept user query and retrieve relevant documents
// =============================================================================
var query = "How do I build web applications with .NET?";
Console.WriteLine("Step 5: Performing semantic search...");
Console.WriteLine($"  Query: \"{query}\"");
Console.WriteLine();

// Embed the query
var queryEmbedding = await embedder.GenerateEmbeddingAsync(query);

// Search for top-K most relevant documents
var topK = 3;
var searchResults = await collection.VectorizedSearchAsync(queryEmbedding.Vector, new VectorSearchOptions { Top = topK });

var retrievedDocs = new List<string>();
Console.WriteLine($"✓ Top {topK} relevant documents:");
var rank = 1;
await foreach (var result in searchResults.Results)
{
    var text = result.Record.Data["text"]?.ToString() ?? "";
    var preview = text.Length > 100 ? text[..97] + "..." : text;
    Console.WriteLine($"  [{rank}] Score: {result.Score:F4}");
    Console.WriteLine($"      {preview}");
    retrievedDocs.Add(text);
    rank++;
}
Console.WriteLine();

// =============================================================================
// Step 6: Send context + query to local LLM for answer generation
// =============================================================================
Console.WriteLine("Step 6: Generating answer with local LLM (Phi-4)...");
Console.WriteLine();

var chatClient = host.Services.GetRequiredService<IChatClient>();

// Build RAG prompt with retrieved context
var context = string.Join("\n\n", retrievedDocs.Select((doc, idx) => $"[{idx + 1}] {doc}"));
var ragPrompt = $"""
You are a helpful assistant answering questions about .NET development.
Use the following context to answer the user's question. If the context doesn't contain relevant information, say so.

Context:
{context}

Question: {query}

Answer:
""";

// Stream the response from the local LLM
Console.WriteLine("Answer:");
await foreach (var chunk in chatClient.CompleteStreamingAsync(ragPrompt))
{
    Console.Write(chunk.Text);
}
Console.WriteLine();
Console.WriteLine();

// =============================================================================
// Step 7: Interactive mode - allow multiple queries
// =============================================================================
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("Interactive Mode - Ask your own questions!");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

while (true)
{
    Console.Write("Your question (or 'exit' to quit): ");
    var userQuery = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(userQuery) || userQuery.Trim().ToLowerInvariant() == "exit")
    {
        Console.WriteLine("\nGoodbye!");
        break;
    }
    
    Console.WriteLine();
    
    // Embed user query
    var userQueryEmbedding = await embedder.GenerateEmbeddingAsync(userQuery);
    
    // Search for relevant documents
    var userSearchResults = await collection.VectorizedSearchAsync(userQueryEmbedding.Vector, new VectorSearchOptions { Top = topK });
    
    var userRetrievedDocs = new List<string>();
    await foreach (var result in userSearchResults.Results)
    {
        var text = result.Record.Data["text"]?.ToString() ?? "";
        userRetrievedDocs.Add(text);
    }
    
    // Build RAG prompt
    var userContext = string.Join("\n\n", userRetrievedDocs.Select((doc, idx) => $"[{idx + 1}] {doc}"));
    var userRagPrompt = $"""
You are a helpful assistant answering questions about .NET development.
Use the following context to answer the user's question. If the context doesn't contain relevant information, say so.

Context:
{userContext}

Question: {userQuery}

Answer:
""";
    
    // Generate answer
    Console.WriteLine("Answer:");
    await foreach (var chunk in chatClient.CompleteStreamingAsync(userRagPrompt))
    {
        Console.Write(chunk.Text);
    }
    Console.WriteLine();
    Console.WriteLine();
}

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    Zero-Cloud RAG Complete!                    ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  This sample demonstrated:                                     ║");
Console.WriteLine("║  ✓ Local embedding generation (no API calls)                   ║");
Console.WriteLine("║  ✓ In-memory vector storage                                    ║");
Console.WriteLine("║  ✓ Semantic search with cosine similarity                      ║");
Console.WriteLine("║  ✓ Local LLM inference (Phi-4)                                 ║");
Console.WriteLine("║  ✓ Complete RAG pipeline without cloud dependencies            ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
