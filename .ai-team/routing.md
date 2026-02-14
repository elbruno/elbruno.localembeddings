# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, API design | Ripley | Public API shape, abstractions, integration patterns |
| ONNX runtime, model loading | Dallas | Model inference, tensor ops, embedding generation |
| M.E.AI integration, DI, config | Kane | IEmbeddingGenerator impl, service registration |
| Tests, edge cases, quality | Lambert | Unit tests, integration tests, model loading tests |
| Code review | Ripley | Review PRs, check quality, suggest improvements |
| Scope & priorities | Ripley | What to build next, trade-offs, decisions |
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Ripley |
| `squad:{name}` | Pick up issue and complete the work | Named member |

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for simple questions.
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn Lambert to write test cases from requirements simultaneously.
