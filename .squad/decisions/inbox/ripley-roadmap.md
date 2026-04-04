# Team Decision: ElBruno.LocalEmbeddings Improvement Roadmap

**By:** Ripley (Lead/Architect)  
**Date:** 2026-02-28  
**Status:** Proposed — Pending stakeholder review

## Context

Analyzed .NET AI community trends (April 2026) and current library capabilities to identify strategic improvements. Key ecosystem changes:
- Microsoft.Extensions.AI 10.4.1 — Unified AI abstractions with middleware patterns
- Microsoft.Extensions.VectorData 10.1.0 — Hybrid search (vector + keyword) now GA
- Microsoft Agent Framework — Multi-agent orchestration standard
- Model Context Protocol (MCP) — Standard for composable AI skills
- Native AOT in .NET 10 — Critical for edge AI and serverless
- Edge SLMs (Phi-3, Llama 3) running locally via ONNX
- AG-UI Protocol — Real-time streaming interactive agent UIs

## Decision

Created comprehensive roadmap at `docs/roadmap.md` with 5 priority tiers covering:
1. **Core improvements** — Streaming APIs, batch progress, embedding cache, dimension reduction
2. **New features** — Native AOT, hybrid search, MCP integration, multi-modal abstraction
3. **New samples** — Agent Framework, Blazor WASM, Semantic Memory, ARM64 optimization
4. **Ecosystem integration** — M.E.AI middleware, VectorData 10.1.0, SK v2 connector
5. **Performance/edge** — ORT 1.24.4, FP16, auto-quantization, WASM deployment

## Recommended New Team Members

1. **Edge/IoT Specialist** — ARM64, WASM, Native AOT, quantization expertise
2. **AI Framework Specialist** — Agent Framework, Semantic Kernel, MCP orchestration
3. **Data/Search Engineer** — Hybrid search, BM25, vector databases, embedding evaluation

## Rationale

- Library has strong foundation but lacks key features community expects (streaming, hybrid search, Native AOT)
- .NET AI ecosystem rapidly evolving — need to stay aligned with M.E.AI/VectorData/Agent Framework
- Edge deployment is critical trend — current team lacks dedicated edge expertise
- MCP and multi-agent patterns are becoming standard — need AI framework specialist

## Impact on Team Workflows

- **Parker (Performance):** Will lead ORT upgrades, FP16 precision, batch size tuning
- **Dallas (Core Dev):** Will implement streaming APIs, batch progress, embedding cache
- **Kane (Integration):** Will lead M.E.AI middleware, VectorData integration
- **Ash (Security):** Will validate Native AOT security implications, MCP trust boundaries
- **New Edge Specialist:** Will own Native AOT, WASM, Raspberry Pi optimization
- **New AI Framework Specialist:** Will own Agent Framework, MCP, SK v2 integration
- **New Data/Search Engineer:** Will own hybrid search, persistent vector stores

## Next Actions

1. Review roadmap with Bruno Capuano (project owner)
2. Prioritize Phase 1 items (Q2 2026): streaming APIs, M.E.AI middleware, ORT upgrade
3. Create tracking issues for each roadmap item
4. Begin recruiting Edge/IoT, AI Framework, and Data/Search specialists
5. Establish quarterly milestones and community engagement metrics

## Open Questions

- Should we prioritize Native AOT over hybrid search for Phase 2?
- Is SQLite the right choice for persistent embedding cache, or should we use custom binary format?
- Should multi-modal abstraction be in core library or separate package?
- Do we need a breaking change policy for roadmap items that affect existing API surface?

---

**Scribe:** Please review and merge into `.squad/decisions.md` after stakeholder approval.
