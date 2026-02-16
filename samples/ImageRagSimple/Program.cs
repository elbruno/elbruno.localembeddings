using ElBruno.LocalEmbeddings.ImageEmbeddings;

Console.WriteLine("=== Image RAG Simple ===");
Console.WriteLine("A minimal demo of CLIP-based text-to-image semantic search.\n");

// ---------------------------------------------------------------
// Step 1: Parse arguments
// ---------------------------------------------------------------
if (args.Length < 2)
{
    Console.WriteLine("Usage: ImageRagSimple <model-directory> <image-directory>");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  model-directory   Directory containing CLIP ONNX models:");
    Console.WriteLine("                      text_model.onnx, vision_model.onnx, vocab.json, merges.txt");
    Console.WriteLine("  image-directory   Directory containing images to search");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run --project samples/ImageRagSimple -- ./clip-models ./my-images");
    Console.WriteLine();
    Console.WriteLine("To get CLIP models, run:");
    Console.WriteLine("  pip install optimum[exporters]");
    Console.WriteLine("  optimum-cli export onnx --model openai/clip-vit-base-patch32 ./clip-models/");
    return;
}

string modelDir = args[0];
string imageDir = args[1];

if (!Directory.Exists(modelDir))
{
    Console.WriteLine($"Error: Model directory not found: {modelDir}");
    return;
}

if (!Directory.Exists(imageDir))
{
    Console.WriteLine($"Error: Image directory not found: {imageDir}");
    return;
}

// ---------------------------------------------------------------
// Step 2: Load CLIP models
// ---------------------------------------------------------------
Console.WriteLine("Step 1: Loading CLIP models...");

string textModelPath = Path.Combine(modelDir, "text_model.onnx");
string visionModelPath = Path.Combine(modelDir, "vision_model.onnx");
string vocabPath = Path.Combine(modelDir, "vocab.json");
string mergesPath = Path.Combine(modelDir, "merges.txt");

using var textEncoder = new ClipTextEncoder(textModelPath, vocabPath, mergesPath);
using var imageEncoder = new ClipImageEncoder(visionModelPath);

Console.WriteLine("  Models loaded successfully.\n");

// ---------------------------------------------------------------
// Step 3: Index images
// ---------------------------------------------------------------
Console.WriteLine("Step 2: Indexing images...");

var searchEngine = new ImageSearchEngine(imageEncoder, textEncoder);
searchEngine.IndexImages(imageDir, (current, total) =>
{
    Console.WriteLine($"  [{current}/{total}] Indexed");
});

Console.WriteLine($"  {searchEngine.ImageCount} images indexed.\n");

if (searchEngine.ImageCount == 0)
{
    Console.WriteLine("No images found. Add images to the directory and try again.");
    return;
}

// ---------------------------------------------------------------
// Step 4: Search with sample queries
// ---------------------------------------------------------------
Console.WriteLine("Step 3: Running sample queries...\n");

string[] sampleQueries =
[
    "a cat",
    "a sunset over the ocean",
    "a person riding a bicycle",
    "a red car"
];

foreach (var query in sampleQueries)
{
    var results = searchEngine.SearchByText(query, topK: 3);

    Console.WriteLine($"Query: \"{query}\"");
    Console.WriteLine(new string('-', 50));

    for (int i = 0; i < results.Count; i++)
    {
        var (imagePath, score) = results[i];
        Console.WriteLine($"  {i + 1}. {Path.GetFileName(imagePath)} (score: {score:F4})");
    }

    Console.WriteLine();
}

Console.WriteLine("Done! This demonstrates the basic image RAG workflow:");
Console.WriteLine("  1. Load CLIP models (text + vision encoders)");
Console.WriteLine("  2. Index images by computing their CLIP embeddings");
Console.WriteLine("  3. Search by encoding text queries and comparing with cosine similarity");
