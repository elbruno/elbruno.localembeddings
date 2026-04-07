using ElBruno.LocalEmbeddings.Harrier;
using ElBruno.LocalEmbeddings.Harrier.Options;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       Harrier-OSS-v1 Embedding Sample Console Application     ║");
Console.WriteLine("║    Generate embeddings locally using Microsoft Harrier + ONNX  ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// EXAMPLE 1: Basic usage — generate embeddings with default settings
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 1: Basic Usage with Default Model (Quantized INT8)");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var options = new HarrierEmbeddingsOptions
{
    ModelVariant = HarrierModelVariant.Quantized,
    InstructionPrefix = HarrierEmbeddingsOptions.DefaultInstructionPrefix,
    EnsureModelDownloaded = true
};

Console.WriteLine($"Model: {options.ModelName}");
Console.WriteLine($"Variant: {options.ModelVariant}");
Console.WriteLine($"Max sequence length: {options.MaxSequenceLength}");
Console.WriteLine();

Console.WriteLine("Initializing model (will download ~500 MB on first run)...");
var startTime = DateTime.Now;

var progress = new Progress<double>(p =>
{
    Console.Write($"\r⬇️ Downloading model: {p:P0}   ");
});

await using var generator = await HarrierEmbeddingGenerator.CreateAsync(options, progress);
Console.WriteLine();

var loadTime = DateTime.Now - startTime;
Console.WriteLine($"✓ Model loaded in {loadTime.TotalSeconds:F2} seconds");
Console.WriteLine($"  Provider: {generator.Metadata.ProviderName}");
Console.WriteLine($"  Model: {generator.Metadata.DefaultModelId}");
Console.WriteLine($"  Embedding dimensions: {generator.Metadata.DefaultModelDimensions}");
Console.WriteLine();

// =============================================================================
// EXAMPLE 2: Generate a single embedding
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 2: Generate Embedding for a Single String");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var singleText = "The quick brown fox jumps over the lazy dog.";
Console.WriteLine($"Input text: \"{singleText}\"");
Console.WriteLine();

var singleEmbedding = await generator.GenerateAsync([singleText]);
var vector = singleEmbedding[0].Vector;

Console.WriteLine($"✓ Generated embedding with {vector.Length} dimensions");
Console.WriteLine($"  First 5 values: [{string.Join(", ", vector.ToArray().Take(5).Select(v => v.ToString("F6")))}...]");
Console.WriteLine($"  Vector norm: {Math.Sqrt(vector.ToArray().Sum(v => (double)v * v)):F6}");
Console.WriteLine();

// =============================================================================
// EXAMPLE 3: Batch embedding generation
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
for (int i = 0; i < documents.Count; i++)
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
// EXAMPLE 4: Cosine similarity between sentence pairs
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 4: Semantic Similarity with Cosine Similarity");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var sentencePairs = new (string, string)[]
{
    ("I love programming", "I enjoy coding"),
    ("I love programming", "The weather is nice today"),
    ("Machine learning is fascinating", "AI and ML are interesting fields"),
    ("The cat sat on the mat", "A feline rested on the rug")
};

Console.WriteLine("Comparing sentence pairs:");
Console.WriteLine();

foreach (var (sentence1, sentence2) in sentencePairs)
{
    var embeddings = await generator.GenerateAsync([sentence1, sentence2]);
    double similarity = CosineSimilarity(embeddings[0].Vector.ToArray(), embeddings[1].Vector.ToArray());

    string similarityBar = new('█', (int)(similarity * 20));
    string emptyBar = new('░', 20 - (int)(similarity * 20));

    Console.WriteLine($"  \"{sentence1}\"");
    Console.WriteLine($"  \"{sentence2}\"");
    Console.WriteLine($"  Similarity: [{similarityBar}{emptyBar}] {similarity:P1}");
    Console.WriteLine();
}

// =============================================================================
// EXAMPLE 5: Instruction prefix customization
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 5: Custom Instruction Prefixes");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

Console.WriteLine("Harrier is an instruction-tuned model. The instruction prefix");
Console.WriteLine("guides the model to produce task-optimized embeddings.");
Console.WriteLine();
Console.WriteLine("Common instruction prefixes:");
Console.WriteLine("  • Retrieval: \"Instruct: Retrieve semantically similar text\\nQuery: \"");
Console.WriteLine("  • Web search: \"Instruct: Given a web search query, retrieve relevant passages...\\nQuery: \"");
Console.WriteLine("  • Classification: \"Instruct: Classify the following text\\nQuery: \"");
Console.WriteLine("  • Clustering: \"Instruct: Identify the topic or theme...\\nQuery: \"");
Console.WriteLine();
Console.WriteLine("The default prefix is set for retrieval/similarity tasks.");
Console.WriteLine("To use a different prefix, set options.InstructionPrefix when creating the generator.");
Console.WriteLine();

// =============================================================================
// EXAMPLE 6: Token counting
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Example 6: Token Counting");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var textsToCount = new[]
{
    "Hello world!",
    "Machine learning is a subset of artificial intelligence.",
    "The Harrier-OSS-v1 model supports 94+ languages with up to 32K token context windows."
};

foreach (string text in textsToCount)
{
    int tokenCount = generator.CountTokens(text);
    Console.WriteLine($"  \"{text}\"");
    Console.WriteLine($"  → {tokenCount} tokens (including BOS/EOS + instruction prefix)");
    Console.WriteLine();
}

// =============================================================================
// Summary
// =============================================================================
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                      Sample Complete!                          ║");
Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Harrier-OSS-v1 provides:                                     ║");
Console.WriteLine("║  • #1 MTEB-v2 ranked embedding model                          ║");
Console.WriteLine("║  • 94+ language support                                        ║");
Console.WriteLine("║  • 640-dim embeddings (270M model)                             ║");
Console.WriteLine("║  • Instruction-tuned for task-specific embeddings              ║");
Console.WriteLine("║  • Multiple ONNX variants (fp32, fp16, int8, q4)              ║");
Console.WriteLine("║  • Automatic model download and caching                        ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");

return;

static double CosineSimilarity(float[] a, float[] b)
{
    double dot = 0, normA = 0, normB = 0;
    for (int i = 0; i < a.Length; i++)
    {
        dot += a[i] * b[i];
        normA += a[i] * (double)a[i];
        normB += b[i] * (double)b[i];
    }
    return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
}
