# Bishop — History

## Project Context

- **Project:** ElBruno.LocalEmbeddings — .NET library for local embedding generation using ONNX Runtime and Microsoft.Extensions.AI
- **Owner:** Bruno Capuano
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI 10.4.1, ONNX Runtime, HuggingFace models
- **Ecosystem:** Microsoft.Extensions.VectorData 10.1.0, Semantic Kernel 1.74.0, Microsoft Agent Framework
- **Key focus areas:** MCP integration, Agent Framework samples, SK memory connector, M.E.AI middleware

## Key Files

- `src/ElBruno.LocalEmbeddings/` — Core library implementing IEmbeddingGenerator
- `src/ElBruno.LocalEmbeddings.KernelMemory/` — Existing Kernel Memory integration
- `src/ElBruno.LocalEmbeddings.VectorData/` — Existing VectorData integration
- `docs/roadmap.md` — Improvement roadmap (Priority 2.3 MCP, 2.4 SLM, 3.1 Agent Framework, 3.3 AG-UI, 4.1-4.4)

## Learnings

### 2026-04-04: Sample Application Patterns

Created three new sample applications demonstrating LocalEmbeddings usage patterns:

**ZeroCloudRag** (`samples/ZeroCloudRag/`):
- Demonstrates semantic search foundation for RAG systems
- Shows document embedding, semantic retrieval with `FindClosestAsync`
- Includes document similarity comparison
- 100% offline operation with no LLM integration (kept simple)
- Target: `net10.0`, references only `ElBruno.LocalEmbeddings`

**McpToolRouter** (`samples/McpToolRouter/`):
- Demonstrates MCP tool routing pattern using embeddings
- Shows semantic tool discovery without manual keyword mapping
- Uses tuple-based tool definitions `(string Name, string Description)`
- Simplified implementation using `FindClosestAsync` directly
- Demonstrates ~10ms routing performance

**LocalLlmRag** (`samples/LocalLlmRag/`):
- Basic embeddings integration example
- Shows semantic search with multiple queries
- Demonstrates cosine similarity comparisons
- Simplified to focus on embeddings only (no LLM dependency)
- Note for users: Install `ElBruno.LocalLLMs` separately for LLM integration

**Key Design Decisions**:
1. All samples target `net10.0` for consistency with existing samples
2. Used `<ProjectReference>` for source projects (not NuGet packages)
3. Avoided external packages that don't exist yet (e.g., `ElBruno.LocalLLMs.Rag`, `ElBruno.ModelContextProtocol.MCPToolRouter`)
4. Kept samples simple and focused on core LocalEmbeddings functionality
5. All samples use top-level statements (no explicit Main method)
6. Record types must be declared before top-level statements in C# 10

**Challenges**:
- Unknown APIs for `ElBruno.LocalLLMs` - opted to keep samples focused on embeddings only
- `ElBruno.ModelContextProtocol.MCPToolRouter` package doesn't exist - implemented pattern directly
- Simplified from original complex roadmap specifications to working, buildable samples

**Outcome**: All 3 samples build successfully and demonstrate practical LocalEmbeddings usage patterns.
