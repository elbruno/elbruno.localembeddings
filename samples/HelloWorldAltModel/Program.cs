using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L12-v2",
    EnsureModelDownloaded = true
};

using var generator = new LocalEmbeddingGenerator(options);

var result = await generator.GenerateAsync(["Hello world from a non-default embeddings model!"]);

Console.WriteLine($"Model: {options.ModelName}");
Console.WriteLine($"Dimensions: {result[0].Vector.Length}");
