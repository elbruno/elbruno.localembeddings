# Roadmap Update — ElBruno Ecosystem Integration

**Date:** 2026-04-04  
**By:** Ripley (Lead/Architect)  
**Requested by:** Bruno Capuano  
**Status:** Completed  
**Commit:** 35d2daa

## Summary

Updated `docs/roadmap.md` to remove all Semantic Kernel references and integrate existing ElBruno ecosystem libraries (LocalLLMs, ModelContextProtocol). Added two new sample scenarios showcasing zero-cloud AI with the full ElBruno stack.

## Changes Made

### 1. Removed Semantic Kernel Items

**Deleted entirely:**
- ~~4.3 Semantic Kernel v2 Memory Connector~~ — Bruno requested removal
- ~~3.4 Semantic Memory + Persistent Vector Store~~ — Renamed to "Persistent Vector Store Sample" (without SK dependency)

**Removed SK references from:**
- 3.1 Agent Framework sample dependencies (was "Microsoft.Extensions.AI.Agents or Semantic Kernel multi-agent APIs")

### 2. Updated MCP Integration (Item 2.3)

**Before:** New `ElBruno.LocalEmbeddings.Mcp` package  
**After:** Coordinate with existing **ElBruno.ModelContextProtocol** library

**Key Context:**
- Bruno owns `ElBruno.ModelContextProtocol.MCPToolRouter` v0.1.0
- Already uses LocalEmbeddings for semantic tool routing
- Does NOT yet expose MCP server tools (`embed_text`, `search_embeddings`)

**Work Split:**
- THIS repo: Ensure API is clean for MCP integration, add convenience methods if needed
- ElBruno.ModelContextProtocol repo: Add MCP server tools (tracked via GitHub issue on that repo)

**Effort:** M → S (API review only)

### 3. Updated SLM Integration (Item 2.4)

**Before:** Generic "Phi-3 via ONNX" sample  
**After:** Reference **ElBruno.LocalLLMs** for zero-cloud RAG

**Key Context:**
- `ElBruno.LocalLLMs` v0.9.0 + `ElBruno.LocalLLMs.Rag` v0.1.0
- `LocalChatClient` implements `IChatClient` via ONNX Runtime GenAI
- Supports: Phi-3.5 mini, Phi-4, Llama 3.x, Qwen2.5, Mistral, Gemma, DeepSeek-R1
- **LocalLLMs.Rag** already supports `IEmbeddingGenerator<string, Embedding<float>>`

**Sample Goals:**
- Zero-cloud RAG: ElBruno.LocalEmbeddings → ElBruno.LocalLLMs
- DI registration pattern combining both libraries
- Fully offline AI

**Effort:** M → S (packages already published)

### 4. Updated Agent Framework Sample (Item 3.1)

**Before:** Microsoft Agent Framework + local embeddings (generic SK references)  
**After:** Agent Framework + **ElBruno.LocalEmbeddings** + **ElBruno.LocalLLMs**

**Implementation:**
- LocalEmbeddings for semantic document retrieval
- LocalLLMs `LocalChatClient` for agent responses
- Full offline multi-agent: retrieve → summarize → answer

### 5. New Sample: Zero-Cloud RAG with ElBruno Stack (Item 3.5)

**New item under Priority 3:**

- Combines **ElBruno.LocalEmbeddings** + **ElBruno.LocalLLMs** + **ElBruno.LocalEmbeddings.VectorData**
- Full offline RAG pipeline: local embeddings → in-memory vector store → local LLM generation
- No cloud APIs, no internet required
- DI registration pattern showing all three libraries
- Target models: all-MiniLM (embeddings) + Phi-4 (generation)

**Effort:** S

### 6. New Sample: MCP Tool Router Sample (Item 3.6)

**New item under Priority 3:**

- Sample showing **ElBruno.ModelContextProtocol.MCPToolRouter** with LocalEmbeddings
- Semantic tool discovery and routing for AI agents
- Show `ToolRouter`, `ToolIndex` integration
- Natural language queries → semantic routing to right tool

**Effort:** S

### 7. Updated Team Recommendations

**AI Framework Specialist (role 2):**
- **Removed:** "Semantic Kernel" from expertise
- **Added:** "ElBruno ecosystem integration"
- **Updated Priorities:** 2.3, 2.4, 3.1, 3.3, 3.5, 3.6, 4.3

### 8. Renumbered Items

After removals and additions:
- 3.4: Persistent Vector Store Sample (was Semantic Memory)
- 3.5: Zero-Cloud RAG (NEW)
- 3.6: MCP Tool Router (NEW)
- 3.7: ARM64 / Raspberry Pi (was 3.5)
- 4.3: Foundry Local Agent (was 4.4)

### 9. Updated Implementation Phasing

**Phase 3 (Q4 2026):** 2.3, 2.4, 3.1, 3.3, 3.5, 3.6 (added 3.5, 3.6)  
**Phase 4 (Q1 2027):** 1.3, 3.2, 3.4, 3.7, 5.2, 5.3, 5.5 (updated numbering)

## Strategic Insight

Bruno owns three libraries that form a **complete zero-cloud AI stack**:

1. **ElBruno.LocalEmbeddings** — Text/image embeddings via ONNX, M.E.AI `IEmbeddingGenerator`
2. **ElBruno.LocalLLMs** — Local chat via ONNX Runtime GenAI, M.E.AI `IChatClient`, includes RAG pipeline
3. **ElBruno.ModelContextProtocol** — Semantic tool routing using LocalEmbeddings

The roadmap now emphasizes **zero-cloud AI** as a first-class scenario, showcasing the full ElBruno ecosystem working together. No external APIs, no internet required — fully offline RAG, multi-agent, and tool routing.

## Decision for Team

- **Respect ElBruno ecosystem boundaries:** MCP server tools go in ElBruno.ModelContextProtocol, not here
- **Zero-cloud AI is a priority scenario:** Samples should showcase LocalEmbeddings + LocalLLMs integration
- **No new SK dependencies:** Roadmap no longer assumes Semantic Kernel; use Microsoft.Extensions.AI and Agent Framework instead
- **Coordinate across Bruno's repos:** LocalEmbeddings, LocalLLMs, ModelContextProtocol are complementary

## Next Actions

1. Create GitHub issue on ElBruno.ModelContextProtocol for MCP server tools (embed_text, search_embeddings, get_embedding_model_status)
2. Prioritize zero-cloud RAG sample (3.5) in Phase 3 implementation
3. Update Phase 3 tracking issues to include new items 3.5 and 3.6
4. Validate that ElBruno.LocalLLMs.Rag integration is clean (it already supports IEmbeddingGenerator)
