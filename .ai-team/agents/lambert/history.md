# Project Context

- **Owner:** Bruno Capuano (bcapuano@gmail.com)
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-12: Test Suite Created
- Created comprehensive unit tests in `tests/LocalEmbeddings.Tests/`
- Test files: `ModelDownloaderTests.cs`, `TokenizerTests.cs`, `LocalEmbeddingGeneratorTests.cs`
- Uses xUnit, Moq for mocking, Xunit.SkippableFact for conditional skipping
- Integration tests marked with `[Trait("Category", "Integration")]` for CI filtering
- Unit tests (non-integration) can run without model files by using mocks
- Run unit tests: `dotnet test --filter "Category!=Integration"`
- Run all tests: `dotnet test` (requires model files in cache)
