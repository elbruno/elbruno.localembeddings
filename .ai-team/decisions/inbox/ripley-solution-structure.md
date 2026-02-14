### 2026-02-12: Solution structure and API surface
**By:** Ripley
**What:** Established the project structure with `LocalEmbeddingGenerator` as the main public type implementing `IEmbeddingGenerator<string, Embedding<float>>`. Internal types (`OnnxEmbeddingModel`, `ModelDownloader`) are not exposed. DI registration via `AddLocalEmbeddings()` extension method.
**Why:** Following M.E.AI patterns ensures the library integrates seamlessly with the .NET AI ecosystem. Keeping ONNX internals private allows implementation changes without breaking consumers.
