# Sample Application Design Patterns

**By:** Bishop (AI Framework Specialist)  
**Date:** 2026-04-04  
**Status:** Implemented

## Decision

Created three new sample applications following simplified, focused design patterns:

### Sample Design Principles

1. **Focus on Core Functionality**
   - Each sample demonstrates one primary concept
   - Avoid dependencies on packages that don't exist yet
   - Use only proven, stable APIs

2. **Consistent Structure**
   - All samples target `net10.0`
   - Use `<ProjectReference>` for source projects
   - Top-level statements for simplicity
   - Include README.md with clear prerequisites

3. **Offline-First**
   - All samples run 100% offline after model download
   - No API keys required
   - Models auto-download on first run

### Implemented Samples

**ZeroCloudRag (3.5)** — Semantic search foundation
- Demonstrates RAG retrieval pipeline without LLM complexity
- Shows `FindClosestAsync` for top-K document retrieval
- Includes document similarity matrix
- Simple, direct instantiation (no DI)

**McpToolRouter (3.6)** — Tool routing pattern
- Demonstrates semantic tool discovery
- Uses tuple-based tool definitions
- Implements routing with embeddings directly
- ~10ms routing performance demonstration

**LocalLlmRag (2.4)** — Embeddings integration
- Basic semantic search demonstration
- Multiple query examples
- Cosine similarity comparisons
- Note for LLM integration via separate package

### Key Constraints Applied

- **No hypothetical packages**: Removed references to `ElBruno.LocalLLMs.Rag`, `ElBruno.ModelContextProtocol.MCPToolRouter`
- **No unknown APIs**: Simplified samples when target API was uncertain
- **Build verification**: All samples must build successfully before commit

## Rationale

The original roadmap items referenced packages and APIs that don't exist yet. Rather than create placeholder implementations or wait for those packages, we implemented the core patterns using proven LocalEmbeddings APIs. This delivers immediate value to users while keeping samples maintainable.

## Impact

- Users have three new working samples to learn from
- Patterns demonstrate LocalEmbeddings capabilities clearly
- Samples can be extended later when additional packages become available
- Clean, focused examples that build successfully

## Future Considerations

- When `ElBruno.LocalLLMs` API stabilizes, enhance samples with LLM integration
- If `MCPToolRouter` package ships, update McpToolRouter sample to use it
- Consider adding AG-UI sample when that infrastructure is ready
- Roadmap items 3.1 (Agent Framework) and 3.3 (AG-UI Protocol) still pending
