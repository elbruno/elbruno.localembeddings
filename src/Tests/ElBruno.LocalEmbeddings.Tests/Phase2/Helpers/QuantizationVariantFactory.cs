using System;
using System.Collections.Generic;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;

/// <summary>
/// Factory for generating quantization variant configurations for testing.
/// Supports INT8, Float16, and Float32 variants with metadata.
/// </summary>
public static class QuantizationVariantFactory
{
    public enum QuantizationFormat
    {
        Float32,   // Full precision baseline
        Float16,   // Half precision
        Int8,      // Integer quantization
        Int4,      // 4-bit quantization (future)
    }

    public class QuantizationVariant
    {
        public string ModelName { get; set; } = string.Empty;
        public QuantizationFormat Format { get; set; }
        public double ExpectedMinAccuracy { get; set; }
        public double ExpectedSpeedupRatio { get; set; }
        public double ExpectedMemoryRatio { get; set; }
        public string FileSuffix { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Generates all quantization variants for a given model.
    /// </summary>
    public static List<QuantizationVariant> GenerateVariantsForModel(string modelName)
    {
        return new List<QuantizationVariant>
        {
            new QuantizationVariant
            {
                ModelName = modelName,
                Format = QuantizationFormat.Float32,
                ExpectedMinAccuracy = 1.0,
                ExpectedSpeedupRatio = 1.0,
                ExpectedMemoryRatio = 1.0,
                FileSuffix = "",
                Description = "Full precision baseline"
            },
            new QuantizationVariant
            {
                ModelName = modelName,
                Format = QuantizationFormat.Float16,
                ExpectedMinAccuracy = 0.99,
                ExpectedSpeedupRatio = 1.5,  // 1.5-2x speedup expected
                ExpectedMemoryRatio = 0.5,    // ~50% memory
                FileSuffix = "-fp16",
                Description = "Half precision floating point"
            },
            new QuantizationVariant
            {
                ModelName = modelName,
                Format = QuantizationFormat.Int8,
                ExpectedMinAccuracy = 0.99,
                ExpectedSpeedupRatio = 2.5,  // 2-3x speedup expected
                ExpectedMemoryRatio = 0.25,   // ~25% memory
                FileSuffix = "-int8",
                Description = "8-bit integer quantization"
            },
        };
    }

    /// <summary>
    /// Generates test scenarios for accuracy validation.
    /// Returns tuples of (model, preferQuantized, expectedMinSimilarity)
    /// </summary>
    public static List<(string Model, bool PreferQuantized, QuantizationFormat Format, double MinSimilarity)> GenerateAccuracyTestScenarios()
    {
        return new List<(string, bool, QuantizationFormat, double)>
        {
            ("all-minilm-l6-v2", false, QuantizationFormat.Float32, 1.0),      // Baseline
            ("all-minilm-l6-v2", true, QuantizationFormat.Int8, 0.99),         // INT8 accuracy target
            ("all-minilm-l6-v2", true, QuantizationFormat.Float16, 0.99),      // Float16 accuracy target
            ("e5-small", false, QuantizationFormat.Float32, 1.0),              // Another model baseline
            ("e5-small", true, QuantizationFormat.Int8, 0.98),                 // INT8 with slightly lower threshold
        };
    }

    /// <summary>
    /// Generates performance test scenarios for speedup validation.
    /// Returns tuples of (model, format, expectedSpeedupRatio, tolerance)
    /// </summary>
    public static List<(string Model, QuantizationFormat Format, double MinSpeedupRatio, double Tolerance)> GenerateSpeedupTestScenarios()
    {
        return new List<(string, QuantizationFormat, double, double)>
        {
            ("all-minilm-l6-v2", QuantizationFormat.Float32, 1.0, 0.1),    // Baseline ±10%
            ("all-minilm-l6-v2", QuantizationFormat.Float16, 1.4, 0.2),    // 1.4-1.8x expected
            ("all-minilm-l6-v2", QuantizationFormat.Int8, 2.0, 0.3),       // 1.7-2.3x expected
        };
    }

    /// <summary>
    /// Generates memory usage test scenarios.
    /// Returns tuples of (model, format, expectedMemoryRatio, tolerance)
    /// </summary>
    public static List<(string Model, QuantizationFormat Format, double MemoryRatio, double Tolerance)> GenerateMemoryTestScenarios()
    {
        return new List<(string, QuantizationFormat, double, double)>
        {
            ("all-minilm-l6-v2", QuantizationFormat.Float32, 1.0, 0.1),    // 100% ±10%
            ("all-minilm-l6-v2", QuantizationFormat.Float16, 0.5, 0.15),   // 50% ±15%
            ("all-minilm-l6-v2", QuantizationFormat.Int8, 0.25, 0.1),      // 25% ±10%
        };
    }

    /// <summary>
    /// Generates error scenario test cases for quantization.
    /// </summary>
    public static List<(string Description, string Model, bool PreferQuantized, string ExpectedErrorType)> GenerateErrorScenarios()
    {
        return new List<(string, string, bool, string)>
        {
            ("Missing quantized variant falls back to full", "all-minilm-l6-v2", true, "fallback"),
            ("Corrupted quantized model recovers", "all-minilm-l6-v2", true, "recovery"),
            ("Invalid quantization format handled", "all-minilm-l6-v2", true, "invalid_format"),
        };
    }

    /// <summary>
    /// Gets the model file suffix for a quantization format.
    /// </summary>
    public static string GetModelFileSuffix(QuantizationFormat format)
    {
        return format switch
        {
            QuantizationFormat.Float32 => "",
            QuantizationFormat.Float16 => "-fp16",
            QuantizationFormat.Int8 => "-int8",
            QuantizationFormat.Int4 => "-int4",
            _ => throw new ArgumentException($"Unknown format: {format}")
        };
    }

    /// <summary>
    /// Describes the quantization format for logging.
    /// </summary>
    public static string DescribeFormat(QuantizationFormat format)
    {
        return format switch
        {
            QuantizationFormat.Float32 => "Full Precision (Float32)",
            QuantizationFormat.Float16 => "Half Precision (Float16)",
            QuantizationFormat.Int8 => "8-bit Integer (INT8)",
            QuantizationFormat.Int4 => "4-bit Integer (INT4)",
            _ => "Unknown"
        };
    }
}
