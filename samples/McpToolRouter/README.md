# MCP Tool Router Sample

Demonstrates semantic tool discovery and routing using the Model Context Protocol (MCP) with local embeddings.

## What This Sample Does

This sample shows how to use `ElBruno.ModelContextProtocol.MCPToolRouter` to:

1. **Create MCP tool definitions** with names, descriptions, and parameters
2. **Index tools** using semantic embeddings (no manual keyword tagging needed)
3. **Route natural language queries** to the most relevant tools using similarity search
4. **Provide confidence scores** for routing decisions
5. **Support interactive queries** to test tool discovery in real-time

## Technologies Used

- **ElBruno.LocalEmbeddings** — Local embedding generation with ONNX Runtime
- **ElBruno.ModelContextProtocol.MCPToolRouter** — Semantic tool routing and discovery

## Prerequisites

- .NET 10.0 SDK or later
- Approximately 80 MB of disk space for the embedding model (downloaded automatically on first run)
- Models will be cached in:
  - Windows: `%LOCALAPPDATA%\ElBruno\LocalEmbeddings\models\`
  - Linux/macOS: `~/.local/share/ElBruno/LocalEmbeddings/models/`

## How to Run

```bash
cd samples/McpToolRouter
dotnet run
```

On first run, the application will automatically download the embedding model. Subsequent runs will use the cached model.

## Sample Output

The application will:
1. Create 10 mock MCP tool definitions (search, email, file operations, etc.)
2. Index them using local embeddings
3. Test routing with 10 example queries
4. Show the top 3 matching tools for each query with similarity scores
5. Enter interactive mode for testing your own queries

## How It Works

### Traditional Approach (Keyword Matching)
```
Query: "I need to find information online"
→ Manual keyword mapping: "find" → search_web
→ Brittle, requires explicit rules
```

### Semantic Approach (This Sample)
```
Query: "I need to find information online"
→ Embed query with LocalEmbeddings
→ Compare to all tool embeddings
→ search_web (score: 0.8432)
→ Works with paraphrases, synonyms, concepts
```

## Use Cases

- **AI Agent Frameworks** — Route user requests to the right tool/capability
- **Multi-Agent Systems** — Dynamic capability discovery across agents
- **Plugin Systems** — Semantic plugin discovery without hardcoded mappings
- **Chatbots** — Intent recognition and action routing
- **API Gateways** — Route requests to microservices based on description

## Key Features

- **Semantic Matching** — Understands meaning, not just keywords
- **Fast Routing** — ~10ms per query after initial indexing
- **No Training Required** — Works with any tool descriptions out of the box
- **100% Local** — No cloud services or API keys needed
- **MCP Standard** — Compatible with Model Context Protocol tools

## Learn More

- [LocalEmbeddings Documentation](../../docs/getting-started.md)
- [MCP Tool Router API Reference](https://github.com/elbruno/ElBruno.ModelContextProtocol)
- [Model Context Protocol Specification](https://modelcontextprotocol.io)
