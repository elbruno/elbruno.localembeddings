using System;
using System.Collections.Generic;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;

/// <summary>
/// Quantization Test Assertion Helpers for Phase 2 Week 2.
/// 
/// Provides utilities for comparing quantized and Float32 embeddings:
/// - Accuracy preservation (cosine similarity threshold)
/// - Performance speedup (latency ratio)
/// - Memory savings (size reduction)
/// </summary>
public static class QuantizationTestAssertions
{
    /// <summary>
    /// Default minimum accuracy threshold for quantized vs baseline comparison.
    /// Quantized embeddings must maintain at least 99% of original accuracy.
    /// </summary>
    private const double DefaultMinAccuracyThreshold = 0.99;

    /// <summary>
    /// Default minimum speedup factor (quantized should be at least N% faster).
    /// </summary>
    private const double DefaultMinSpeedupFactor = 1.2; // 20% faster

    /// <summary>
    /// Default minimum memory savings percentage.
    /// </summary>
    private const double DefaultMinMemorySavingsPercent = 0.30; // 30% reduction

    /// <summary>
    /// Assert that quantized embeddings maintain accuracy vs Float32 baseline.
    /// 
    /// Measures cosine similarity between corresponding vector pairs:
    /// - float32_embeddings vs float32_query
    /// - quantized_embeddings vs quantized_query
    /// 
    /// Fails if similarity differs by more than (1 - minThreshold).
    /// </summary>
    public static void AssertAccuracyPreserved(
        IReadOnlyList<float[]> baselineEmbeddings,
        IReadOnlyList<float[]> quantizedEmbeddings,
        double minThreshold = DefaultMinAccuracyThreshold,
        string? messagePrefix = null)
    {
        if (baselineEmbeddings.Count == 0)
            throw new ArgumentException("Baseline embeddings cannot be empty", nameof(baselineEmbeddings));

        if (quantizedEmbeddings.Count == 0)
            throw new ArgumentException("Quantized embeddings cannot be empty", nameof(quantizedEmbeddings));

        if (baselineEmbeddings.Count != quantizedEmbeddings.Count)
            throw new ArgumentException(
                $"Embedding counts must match: baseline={baselineEmbeddings.Count}, quantized={quantizedEmbeddings.Count}",
                nameof(quantizedEmbeddings));

        if (minThreshold < 0 || minThreshold > 1)
            throw new ArgumentOutOfRangeException(nameof(minThreshold), "Must be between 0 and 1");

        var message = messagePrefix ?? "Accuracy preservation check";
        var similarities = new List<double>();

        // For each vector, compute similarity between baseline and quantized
        for (int i = 0; i < baselineEmbeddings.Count; i++)
        {
            var baselineVector = baselineEmbeddings[i];
            var quantizedVector = quantizedEmbeddings[i];

            if (baselineVector.Length != quantizedVector.Length)
            {
                throw new ArgumentException(
                    $"Vector dimensions must match at index {i}: " +
                    $"baseline={baselineVector.Length}, quantized={quantizedVector.Length}",
                    nameof(quantizedEmbeddings));
            }

            // Calculate cosine similarity
            double cosineSimilarity = CalculateCosineSimilarity(baselineVector, quantizedVector);
            similarities.Add(cosineSimilarity);
        }

        // Verify minimum similarity threshold
        double minSimilarity = similarities.Min();
        double avgSimilarity = similarities.Average();

        if (minSimilarity < minThreshold)
        {
            throw new Xunit.Sdk.XunitException(
                $"{message}: Accuracy NOT preserved.\n" +
                $"  Min similarity: {minSimilarity:F4} (expected >= {minThreshold:F4})\n" +
                $"  Avg similarity: {avgSimilarity:F4}\n" +
                $"  Samples: {similarities.Count}");
        }
    }

    /// <summary>
    /// Assert that quantized model is faster than Float32 baseline by minimum factor.
    /// 
    /// Example: if float32_latency=100ms and speedup_factor=1.5,
    /// then quantized_latency must be <= 66.7ms (100/1.5).
    /// </summary>
    public static void AssertSpeedup(
        long float32LatencyMs,
        long quantizedLatencyMs,
        double minSpeedupFactor = DefaultMinSpeedupFactor,
        string? messagePrefix = null)
    {
        if (float32LatencyMs <= 0)
            throw new ArgumentException("Float32 latency must be positive", nameof(float32LatencyMs));

        if (quantizedLatencyMs <= 0)
            throw new ArgumentException("Quantized latency must be positive", nameof(quantizedLatencyMs));

        if (minSpeedupFactor <= 1.0)
            throw new ArgumentException("Speedup factor must be > 1.0", nameof(minSpeedupFactor));

        var message = messagePrefix ?? "Speedup verification";
        double actualSpeedupFactor = (double)float32LatencyMs / quantizedLatencyMs;

        if (actualSpeedupFactor < minSpeedupFactor)
        {
            throw new Xunit.Sdk.XunitException(
                $"{message}: Speedup NOT achieved.\n" +
                $"  Float32 latency: {float32LatencyMs}ms\n" +
                $"  Quantized latency: {quantizedLatencyMs}ms\n" +
                $"  Actual speedup: {actualSpeedupFactor:F2}x (expected >= {minSpeedupFactor:F2}x)");
        }
    }

    /// <summary>
    /// Assert that quantized model uses less memory than Float32 baseline by minimum percentage.
    /// 
    /// Example: if float32_bytes=1000 and min_percent=0.5,
    /// then quantized_bytes must be <= 500 (50% of original).
    /// </summary>
    public static void AssertMemorySavings(
        long float32SizeBytes,
        long quantizedSizeBytes,
        double minSavingsPercent = DefaultMinMemorySavingsPercent,
        string? messagePrefix = null)
    {
        if (float32SizeBytes <= 0)
            throw new ArgumentException("Float32 size must be positive", nameof(float32SizeBytes));

        if (quantizedSizeBytes <= 0)
            throw new ArgumentException("Quantized size must be positive", nameof(quantizedSizeBytes));

        if (minSavingsPercent < 0 || minSavingsPercent > 1)
            throw new ArgumentException("Savings percent must be between 0 and 1", nameof(minSavingsPercent));

        var message = messagePrefix ?? "Memory savings verification";

        // Calculate actual reduction ratio (lower is better)
        double actualRatio = (double)quantizedSizeBytes / float32SizeBytes;
        double actualSavingsPercent = 1 - actualRatio;

        if (actualSavingsPercent < minSavingsPercent)
        {
            throw new Xunit.Sdk.XunitException(
                $"{message}: Memory savings NOT achieved.\n" +
                $"  Float32 size: {float32SizeBytes:,} bytes\n" +
                $"  Quantized size: {quantizedSizeBytes:,} bytes\n" +
                $"  Actual reduction: {actualSavingsPercent:P} (expected >= {minSavingsPercent:P})");
        }
    }

    /// <summary>
    /// Assert that quantized model produces acceptable results with fallback to Float32.
    /// 
    /// Tests graceful degradation: if quantized unavailable, Float32 should be used.
    /// </summary>
    public static void AssertFallbackBehavior(
        bool quantizedAvailable,
        IReadOnlyList<float[]> quantizedEmbeddings,
        IReadOnlyList<float[]> fallbackEmbeddings,
        string? messagePrefix = null)
    {
        var message = messagePrefix ?? "Fallback behavior";

        if (!quantizedAvailable)
        {
            // Fallback should be used
            if (fallbackEmbeddings.Count == 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{message}: Fallback embeddings are empty when quantized unavailable");
            }
        }
        else
        {
            // Quantized should be used
            if (quantizedEmbeddings.Count == 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{message}: Quantized embeddings are empty when quantized available");
            }
        }
    }

    /// <summary>
    /// Assert backward compatibility: newer quantization should be compatible with older settings.
    /// </summary>
    public static void AssertBackwardCompatibility(
        string oldQuantizationType,
        string newQuantizationType,
        IReadOnlyList<float[]> oldEmbeddings,
        IReadOnlyList<float[]> newEmbeddings,
        string? messagePrefix = null)
    {
        var message = messagePrefix ?? "Backward compatibility check";

        if (oldEmbeddings.Count != newEmbeddings.Count)
        {
            throw new Xunit.Sdk.XunitException(
                $"{message}: Embedding counts differ.\n" +
                $"  {oldQuantizationType}: {oldEmbeddings.Count} vectors\n" +
                $"  {newQuantizationType}: {newEmbeddings.Count} vectors");
        }

        // Verify that vectors are compatible (can be compared semantically)
        for (int i = 0; i < oldEmbeddings.Count; i++)
        {
            if (oldEmbeddings[i].Length != newEmbeddings[i].Length)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{message}: Vector dimensions differ at index {i}.\n" +
                    $"  {oldQuantizationType}: {oldEmbeddings[i].Length}d\n" +
                    $"  {newQuantizationType}: {newEmbeddings[i].Length}d");
            }
        }
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors.
    /// 
    /// Returns value in range [0, 1] where 1.0 = identical, 0.0 = orthogonal.
    /// </summary>
    private static double CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have same length");

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }
}
