using System.Collections.Generic;
using System.Linq;
using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;

/// <summary>
/// Factory for generating test vectors, model configurations, and embedding test data.
/// Used across AOT, Quantization, OpenTelemetry, and Streaming tests to ensure consistency.
/// </summary>
public static class EmbeddingDataFactory
{
    public const int DefaultVectorDimension = 384;
    public const int DefaultBatchSize = 32;

    /// <summary>
    /// Generates deterministic random vectors for reproducible testing.
    /// Uses a fixed seed for consistency across test runs.
    /// </summary>
    public static List<float[]> GenerateTestVectors(int count, int dimension = DefaultVectorDimension, int seed = 42)
    {
        var random = new Random(seed);
        var vectors = new List<float[]>(count);

        for (int i = 0; i < count; i++)
        {
            var vector = new float[dimension];
            for (int j = 0; j < dimension; j++)
            {
                vector[j] = (float)(random.NextDouble() - 0.5) * 2;
            }
            
            // Normalize vector
            float norm = (float)Math.Sqrt(vector.Sum(x => x * x));
            if (norm > 0)
            {
                for (int j = 0; j < dimension; j++)
                {
                    vector[j] /= norm;
                }
            }

            vectors.Add(vector);
        }

        return vectors;
    }

    /// <summary>
    /// Generates semantic text pairs with known similarity scores for accuracy validation.
    /// Returns (text1, text2, expectedSimilarity) tuples.
    /// </summary>
    public static List<(string Text1, string Text2, double ExpectedMinSimilarity)> GenerateSemanticPairs()
    {
        return new List<(string, string, double)>
        {
            ("The cat sat on the mat", "A feline rested on a carpet", 0.85),
            ("Machine learning is a subset of AI", "Artificial intelligence includes machine learning", 0.90),
            ("I love sunny days", "I hate rainy weather", 0.40),
            ("The quick brown fox jumps over the lazy dog", "A fast reddish canine jumps above a sluggish dog", 0.88),
            ("Python is a programming language", "Java is also a programming language", 0.75),
            ("Climate change affects global weather patterns", "Global warming impacts climate systems", 0.92),
            ("The stock market crashed yesterday", "Shares dropped significantly", 0.80),
            ("Coffee is a popular beverage", "Tea is another hot drink", 0.70),
            ("Quantum computing uses quantum mechanics", "Classical computers use binary logic", 0.35),
            ("The internet connects computers worldwide", "Networks link devices globally", 0.82),
        };
    }

    /// <summary>
    /// Generates batch of texts for testing batch operations.
    /// </summary>
    public static List<string> GenerateBatchTexts(int batchSize = DefaultBatchSize)
    {
        var texts = new List<string>(batchSize);
        for (int i = 0; i < batchSize; i++)
        {
            texts.Add($"Sample text number {i}: This is a test embedding input with some variation.");
        }
        return texts;
    }

    /// <summary>
    /// Generates edge case texts for robustness testing.
    /// Includes empty, very long, special characters, multilingual, etc.
    /// </summary>
    public static List<string> GenerateEdgeCaseTexts()
    {
        return new List<string>
        {
            "",  // Empty
            " ",  // Whitespace only
            "a",  // Single character
            new string('x', 1000),  // Very long text
            "Special!@#$%^&*()Characters",  // Special chars
            "Numbers123456789",  // Numbers
            "Multiple   spaces   between   words",  // Multiple spaces
            "\n\t\r  ",  // Whitespace variations
            "Line1\nLine2\nLine3",  // Newlines
            "🎉😀🚀",  // Emoji
        };
    }

    /// <summary>
    /// Generates model configuration options for different scenarios.
    /// Used to test configuration validation and defaults.
    /// </summary>
    public static LocalEmbeddingsOptions GenerateDefaultOptions()
    {
        return new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2",
            CacheDirectory = Path.Combine(Path.GetTempPath(), "embeddings-test-cache"),
            NormalizeEmbeddings = false,
            BatchSize = 32
        };
    }

    /// <summary>
    /// Generates options with specific configuration for testing variations.
    /// </summary>
    public static LocalEmbeddingsOptions GenerateOptionsWithConfiguration(
        string? modelName = null,
        int? batchSize = null,
        bool? preferQuantized = null)
    {
        var options = GenerateDefaultOptions();
        
        if (!string.IsNullOrEmpty(modelName))
            options.ModelName = modelName;
        
        if (batchSize.HasValue)
            options.BatchSize = batchSize.Value;

        if (preferQuantized.HasValue)
            options.PreferQuantized = preferQuantized.Value;

        return options;
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors.
    /// Used for accuracy validation in quantization tests.
    /// </summary>
    public static double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same dimension");

        double dotProduct = 0;
        double norm1 = 0;
        double norm2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            norm1 += vector1[i] * vector1[i];
            norm2 += vector2[i] * vector2[i];
        }

        norm1 = Math.Sqrt(norm1);
        norm2 = Math.Sqrt(norm2);

        if (norm1 == 0 || norm2 == 0)
            return 0;

        return dotProduct / (norm1 * norm2);
    }
}
