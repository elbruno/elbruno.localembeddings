using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L12-v2",
    EnsureModelDownloaded = true
};

using var generator = new LocalEmbeddingGenerator(options);

// Single-string overload — no array wrapping needed
var embedding = await generator.GenerateEmbeddingAsync("Hello world from a non-default embeddings model!");

Console.WriteLine($"Model: {options.ModelName}");
Console.WriteLine($"Dimensions: {embedding.Vector.Length}");
