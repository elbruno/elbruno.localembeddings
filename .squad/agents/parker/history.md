# Parker — History & Learnings

## Project Context

- **Project:** ElBruno.LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI and ONNX Runtime
- **Owner:** Bruno Capuano
- **Stack:** .NET 8.0 / 10.0 (multi-target), C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models, NuGet package distribution
- **Joined:** 2026-02-28

## Key Performance Concerns for This Project

1. **ONNX inference throughput:** The primary bottleneck. Session options (inter/intra op thread counts, execution provider selection) can have large impact.
2. **Model loading latency:** First-call cost from loading and JIT-compiling the ONNX model. Worth measuring cold vs. warm.
3. **Tokenization hot path:** Tokenizer runs per input string — allocation pressure here compounds at scale.
4. **Embedding normalization:** TensorPrimitives is already used for SIMD-accelerated cosine similarity and L2 normalization — confirm all vector math goes through this path.
5. **PreferQuantized model selection:** Quantized/int8 models are smaller and faster for inference — ensure this is the default and benchmark the difference.
6. **Batch embedding:** Single vs. batched inference throughput difference is significant — worth exposing and benchmarking.
7. **NuGet package footprint:** Library consumers care about startup time and package size — track dependency weight.

## Learnings

<!-- Append new learnings here as work progresses -->
