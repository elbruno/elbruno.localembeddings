using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L12-v2",
    EnsureModelDownloaded = true
};

Console.WriteLine("Initializing model (will download if not cached)...");
Console.WriteLine($"  Cache directory: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ElBruno", "LocalEmbeddings", "models")}");

// Track download progress
var progress = new Progress<double>(p =>
{
    Console.Write($"\r⬇️ Downloading model: {p:P0}   ");
});

using var generator = await LocalEmbeddingGenerator.CreateAsync(options, progress);
Console.WriteLine();

// Single-string overload — no array wrapping needed
var embedding = await generator.GenerateEmbeddingAsync("Hello world from a non-default embeddings model!");

Console.WriteLine($"Model: {options.ModelName}");
Console.WriteLine($"Dimensions: {embedding.Vector.Length}");
