# Quantization Model Registry

**Purpose:** Central registry of quantized model variants available for ElBruno.LocalEmbeddings  
**Format:** JSON Schema  
**Location:** `docs/quantization-model-registry.json`  
**Maintainer:** ElBruno team  
**Last Updated:** 2026-05-19

---

## Overview

This registry tracks which HuggingFace models have quantized variants (INT8, Float16) available. Users can reference this to understand:
- Which models support quantization
- Expected accuracy/speed tradeoffs
- How to discover quantized model variants
- Model dimensions and file sizes

---

## JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Quantization Model Registry",
  "description": "Registry of quantized model variants for ElBruno.LocalEmbeddings",
  "type": "object",
  "properties": {
    "version": {
      "type": "string",
      "description": "Schema version (semver)",
      "example": "1.0.0"
    },
    "lastUpdated": {
      "type": "string",
      "description": "Last update timestamp (ISO 8601)",
      "example": "2026-05-19T00:00:00Z"
    },
    "models": {
      "type": "array",
      "description": "Array of registered models with quantization variants",
      "items": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "description": "HuggingFace model name (organization/model)",
            "example": "sentence-transformers/all-MiniLM-L6-v2"
          },
          "dimension": {
            "type": "integer",
            "description": "Embedding dimension (output vector size)",
            "example": 384
          },
          "description": {
            "type": "string",
            "description": "Model description",
            "example": "Lightweight multilingual embedding model"
          },
          "releaseDate": {
            "type": "string",
            "description": "Model release date (ISO 8601)",
            "example": "2021-08-15"
          },
          "variants": {
            "type": "array",
            "description": "Available quantization variants",
            "items": {
              "type": "object",
              "properties": {
                "format": {
                  "type": "string",
                  "enum": ["float32", "int8", "float16"],
                  "description": "Quantization format"
                },
                "file": {
                  "type": "string",
                  "description": "Model filename in HuggingFace repo",
                  "example": "model.onnx"
                },
                "size_mb": {
                  "type": "number",
                  "description": "Compressed file size in MB",
                  "example": 134.5
                },
                "sha256": {
                  "type": "string",
                  "description": "SHA-256 hash of the model file",
                  "example": "abc123def456..."
                },
                "isBaseline": {
                  "type": "boolean",
                  "description": "Whether this is the baseline (Float32 reference)",
                  "example": true
                },
                "accuracy": {
                  "type": "object",
                  "description": "Accuracy metrics on standard benchmarks",
                  "properties": {
                    "stsCorrelation": {
                      "type": "number",
                      "description": "STS (Semantic Textual Similarity) correlation score",
                      "example": 0.865
                    },
                    "mtebAvg": {
                      "type": "number",
                      "description": "Average MTEB benchmark score",
                      "example": 0.742
                    }
                  }
                },
                "performance": {
                  "type": "object",
                  "description": "Performance characteristics",
                  "properties": {
                    "latencyMs": {
                      "type": "number",
                      "description": "Single embedding inference latency (milliseconds)",
                      "example": 2.5
                    },
                    "throughputPerSecond": {
                      "type": "number",
                      "description": "Batch throughput (embeddings per second, 32-size batch)",
                      "example": 12800
                    },
                    "speedupVsFloat32": {
                      "type": "number",
                      "description": "Speed improvement vs Float32 (1.0 = same)",
                      "example": 2.8
                    }
                  }
                },
                "tradeoffs": {
                  "type": "object",
                  "description": "Tradeoff analysis vs Float32 baseline",
                  "properties": {
                    "accuracyDropPercent": {
                      "type": "number",
                      "description": "Accuracy drop vs Float32 (%)",
                      "example": 1.2
                    },
                    "sizeReductionPercent": {
                      "type": "number",
                      "description": "File size reduction vs Float32 (%)",
                      "example": 75
                    }
                  }
                },
                "recommended": {
                  "type": "boolean",
                  "description": "Whether this format is recommended for general use",
                  "example": true
                },
                "useCases": {
                  "type": "array",
                  "description": "Recommended use cases",
                  "items": {
                    "type": "string",
                    "enum": [
                      "serverless",
                      "edge",
                      "mobile",
                      "realtime",
                      "batch",
                      "lowMemory",
                      "fastInference"
                    ]
                  },
                  "example": ["serverless", "realtime", "fastInference"]
                }
              },
              "required": ["format", "file", "size_mb", "sha256"]
            }
          }
        },
        "required": ["name", "dimension", "variants"]
      }
    }
  },
  "required": ["version", "models"]
}
```

---

## Example Registry

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-05-19T11:00:00Z",
  "models": [
    {
      "name": "sentence-transformers/all-MiniLM-L6-v2",
      "dimension": 384,
      "description": "Lightweight multilingual embedding model optimized for speed and accuracy",
      "releaseDate": "2021-08-15",
      "variants": [
        {
          "format": "float32",
          "file": "model.onnx",
          "size_mb": 134.5,
          "sha256": "abc123def456...",
          "isBaseline": true,
          "accuracy": {
            "stsCorrelation": 0.865,
            "mtebAvg": 0.742
          },
          "performance": {
            "latencyMs": 2.8,
            "throughputPerSecond": 11429,
            "speedupVsFloat32": 1.0
          },
          "recommended": false,
          "useCases": ["batch", "accuracy-critical"]
        },
        {
          "format": "int8",
          "file": "model_quantized.onnx",
          "size_mb": 33.6,
          "sha256": "def456ghi789...",
          "accuracy": {
            "stsCorrelation": 0.853,
            "mtebAvg": 0.731
          },
          "performance": {
            "latencyMs": 1.0,
            "throughputPerSecond": 32000,
            "speedupVsFloat32": 2.8
          },
          "tradeoffs": {
            "accuracyDropPercent": 1.2,
            "sizeReductionPercent": 75
          },
          "recommended": true,
          "useCases": ["serverless", "realtime", "fastInference", "lowMemory"]
        },
        {
          "format": "float16",
          "file": "model_fp16.onnx",
          "size_mb": 67.3,
          "sha256": "ghi789jkl012...",
          "accuracy": {
            "stsCorrelation": 0.862,
            "mtebAvg": 0.739
          },
          "performance": {
            "latencyMs": 1.8,
            "throughputPerSecond": 17778,
            "speedupVsFloat32": 1.56
          },
          "tradeoffs": {
            "accuracyDropPercent": 0.4,
            "sizeReductionPercent": 50
          },
          "recommended": true,
          "useCases": ["edge", "mobile", "realtime"]
        }
      ]
    },
    {
      "name": "sentence-transformers/all-mpnet-base-v2",
      "dimension": 768,
      "description": "Larger, more accurate model using MPNet architecture",
      "releaseDate": "2021-10-20",
      "variants": [
        {
          "format": "float32",
          "file": "model.onnx",
          "size_mb": 438.0,
          "sha256": "jkl012mno345...",
          "isBaseline": true,
          "accuracy": {
            "stsCorrelation": 0.889,
            "mtebAvg": 0.778
          },
          "performance": {
            "latencyMs": 8.5,
            "throughputPerSecond": 3765,
            "speedupVsFloat32": 1.0
          },
          "recommended": false,
          "useCases": ["accuracy-critical", "batch"]
        },
        {
          "format": "int8",
          "file": "model_int8.onnx",
          "size_mb": 109.5,
          "sha256": "mno345pqr678...",
          "accuracy": {
            "stsCorrelation": 0.874,
            "mtebAvg": 0.761
          },
          "performance": {
            "latencyMs": 3.2,
            "throughputPerSecond": 10000,
            "speedupVsFloat32": 2.66
          },
          "tradeoffs": {
            "accuracyDropPercent": 1.5,
            "sizeReductionPercent": 75
          },
          "recommended": true,
          "useCases": ["serverless", "realtime", "fastInference"]
        }
      ]
    }
  ]
}
```

---

## Usage in Code

### Programmatic Access

```csharp
// Load registry (future: from embedded resource or URL)
var registryJson = File.ReadAllText("quantization-model-registry.json");
var registry = JsonSerializer.Deserialize<QuantizationRegistry>(registryJson);

// Find model info
var modelInfo = registry.Models.FirstOrDefault(m => 
    m.Name == "sentence-transformers/all-MiniLM-L6-v2");

// Get INT8 variant
var int8Variant = modelInfo?.Variants.FirstOrDefault(v => 
    v.Format == QuantizationFormat.Int8);

// Show tradeoffs to user
Console.WriteLine($"Accuracy drop: {int8Variant?.Tradeoffs.AccuracyDropPercent}%");
Console.WriteLine($"Speed improvement: {int8Variant?.Performance.SpeedupVsFloat32}x");
```

### In Documentation

Reference specific quantization metrics when recommending formats:

```markdown
## Quantized Model Performance

### all-MiniLM-L6-v2 (Recommended for Serverless)

| Format | Size | Speed | Accuracy Loss |
|--------|------|-------|----------------|
| Float32 | 134.5 MB | 1x | baseline |
| **INT8** | **33.6 MB** | **2.8x** | **1.2%** ✅ |
| Float16 | 67.3 MB | 1.56x | 0.4% |

**Recommendation:** Use INT8 for Azure Functions, AWS Lambda, Google Cloud Functions.
```

---

## Maintenance

### When to Update

1. **New quantized model variant released**
   - Test on standard benchmarks
   - Measure performance
   - Add to registry

2. **New model added to supported list**
   - Create quantized variants
   - Add all three formats (Float32 baseline, Int8, Float16)
   - Benchmark and document

3. **Performance optimization discovered**
   - Update latency/throughput metrics
   - Note optimization technique in description

### Contribution Process

1. Fork repository
2. Create new branch: `add-quantization-xxx`
3. Update `docs/quantization-model-registry.json`
4. Include benchmarks in PR description
5. Submit PR for review

---

## Backward Compatibility

**Initial Release (Phase 2):**
- Registry format: v1.0.0
- Versioning follows semver
- Breaking schema changes → major version bump

---

## References

- MTEB Benchmark: https://huggingface.co/spaces/mteb/leaderboard
- STS Benchmark: https://github.com/sentences/embedeval/
- ONNX Model Zoo: https://github.com/onnx/models
- ONNX Quantization: https://github.com/microsoft/onnxruntime-tools

---

**End of Registry Schema**
