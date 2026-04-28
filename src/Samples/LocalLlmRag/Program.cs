using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         Local Embeddings Integration Sample                   ║");
Console.WriteLine("║         Simple semantic search demonstration                   ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

Console.WriteLine("NOTE: This sample demonstrates local embeddings only.");
Console.WriteLine("      For LLM integration, install ElBruno.LocalLLMs separately.");
Console.WriteLine();

// =============================================================================
// Step 1: Initialize local embedding generator
// =============================================================================
Console.WriteLine("Step 1: Initializing local embedding generator...");
Console.WriteLine("  Model: sentence-transformers/all-MiniLM-L6-v2");
Console.WriteLine();

var embeddingOptions = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    EnsureModelDownloaded = true
};

var embedder = await LocalEmbeddingGenerator.CreateAsync(embeddingOptions);
Console.WriteLine($"✓ Embedding generator ready");
Console.WriteLine($"  Dimensions: {embedder.Metadata.DefaultModelDimensions}");
Console.WriteLine();

// =============================================================================
// Step 2: Create a simple knowledge base
// =============================================================================
Console.WriteLine("Step 2: Creating knowledge base...");
Console.WriteLine();

var documents = new List<string>
{
    "The weather in Seattle is rainy and cloudy most of the year. Winter temperatures range from 35-45°F.",
    "Seattle is known for its coffee culture, with Starbucks founding here in 1971 at Pike Place Market.",
    "The Space Needle is Seattle's most iconic landmark, built for the 1962 World's Fair.",
    "Microsoft and Amazon both have headquarters in the Seattle metropolitan area.",
    "Seattle's music scene produced grunge bands like Nirvana, Pearl Jam, and Soundgarden in the 1990s.",
    "Pike Place Market is one of the oldest continuously operated public farmers markets in the United States.",
    "The University of Washington is a major research university located in Seattle.",
    "Seattle-Tacoma International Airport (SEA) is the primary airport serving the region."
};

Console.WriteLine($"Created {documents.Count} documents about Seattle:");
foreach (var doc in documents.Take(3))
{
    var preview = doc.Length > 70 ? doc[..67] + "..." : doc;
    Console.WriteLine($"  • {preview}");
}
Console.WriteLine($"  ... and {documents.Count - 3} more");
Console.WriteLine();

// =============================================================================
// Step 3: Generate embeddings for all documents
// =============================================================================
Console.WriteLine("Step 3: Embedding documents...");

var startTime = DateTime.Now;
var documentEmbeddings = await embedder.GenerateAsync(documents);
var embeddingTime = DateTime.Now - startTime;

Console.WriteLine($"✓ Generated {documentEmbeddings.Count} embeddings in {embeddingTime.TotalMilliseconds:F0}ms");
Console.WriteLine($"  Average: {embeddingTime.TotalMilliseconds / documents.Count:F1}ms per document");
Console.WriteLine();

// =============================================================================
// Step 4: Perform semantic search
// =============================================================================
Console.WriteLine("Step 4: Testing semantic search...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var query = "What are some famous landmarks in Seattle?";
Console.WriteLine($"Query: \"{query}\"");
Console.WriteLine();

// Find the most relevant documents using FindClosest extension method
var topK = 3;
var results = await embedder.FindClosestAsync(query, documents, documentEmbeddings, topK: topK, minScore: 0.1f);

Console.WriteLine($"✓ Found {results.Count} relevant documents:");
for (var i = 0; i < results.Count; i++)
{
    var scoreBar = new string('█', (int)(results[i].Score * 15));
    var emptyBar = new string('░', 15 - (int)(results[i].Score * 15));
    Console.WriteLine($"  [{i + 1}] [{scoreBar}{emptyBar}] {results[i].Score:F4}");
    Console.WriteLine($"      {results[i].Text}");
}
Console.WriteLine();

// =============================================================================
// Step 5: Additional example - different query
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var query2 = "Tell me about the technology industry in Seattle";
Console.WriteLine($"Query: \"{query2}\"");
Console.WriteLine();

var results2 = await embedder.FindClosestAsync(query2, documents, documentEmbeddings, topK: topK, minScore: 0.1f);

Console.WriteLine($"✓ Found {results2.Count} relevant documents:");
for (var i = 0; i < results2.Count; i++)
{
    var scoreBar = new string('█', (int)(results2[i].Score * 15));
    var emptyBar = new string('░', 15 - (int)(results2[i].Score * 15));
    Console.WriteLine($"  [{i + 1}] [{scoreBar}{emptyBar}] {results2[i].Score:F4}");
    Console.WriteLine($"      {results2[i].Text}");
}
Console.WriteLine();

// =============================================================================
// Step 6: Show direct embedding comparison
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();
Console.WriteLine("Step 6: Direct embedding similarity comparison...");
Console.WriteLine();

var sentences = new[]
{
    "Seattle has great coffee shops",
    "The city is known for its coffee culture",
    "The weather is often rainy"
};

var sentenceEmbeddings = await embedder.GenerateAsync(sentences);

Console.WriteLine("Comparing sentences:");
for (var i = 0; i < sentences.Length; i++)
{
    Console.WriteLine($"  [{i}] {sentences[i]}");
}
Console.WriteLine();

Console.WriteLine("Similarity scores:");
for (var i = 0; i < sentences.Length; i++)
{
    for (var j = i + 1; j < sentences.Length; j++)
    {
        var similarity = sentenceEmbeddings[i].CosineSimilarity(sentenceEmbeddings[j]);
        var bar = new string('█', (int)(similarity * 20));
        var empty = new string('░', 20 - (int)(similarity * 20));
        Console.WriteLine($"  [{i}] ↔ [{j}]: [{bar}{empty}] {similarity:F4}");
    }
}
Console.WriteLine();

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          Local Embeddings Integration Complete!               ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  This sample demonstrated:                                     ║");
Console.WriteLine("║  ✓ LocalEmbeddingGenerator for semantic search                 ║");
Console.WriteLine("║  ✓ FindClosest extension method for top-K retrieval            ║");
Console.WriteLine("║  ✓ Cosine similarity for comparing embeddings                  ║");
Console.WriteLine("║  ✓ 100% offline - no cloud dependencies                        ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  For LLM integration, install ElBruno.LocalLLMs package        ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
