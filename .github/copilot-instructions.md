# Copilot Instructions — ElBruno.LocalEmbeddings

## Project Overview

A .NET library for generating text embeddings locally using ONNX Runtime and Microsoft.Extensions.AI abstractions — no external API calls required.

**Repository:** https://github.com/elbruno/elbruno.localembeddings

## Development Workflow

### Required Before Each Commit
- Ensure all changes build successfully: `dotnet build`
- Run tests to verify functionality: `dotnet test`
- Code style is enforced via `.editorconfig` and `EnforceCodeStyleInBuild=true`

### Build and Test Commands
- **Build:** `dotnet build` (from repository root)
- **Test:** `dotnet test` (from repository root)
- **Restore dependencies:** `dotnet restore`
- **Clean:** `dotnet clean`

All commands should be run from the repository root.

### Build Configuration
- Uses .NET 10.0 (`net10.0`)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- Warnings treated as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)
- Code style enforcement in build enabled

## Naming Conventions

- **All** core projects, folders, csproj files, and root namespaces **must** start with `ElBruno.` followed by the project name.
- Examples:
    - Folder: `src/ElBruno.LocalEmbeddings/`
    - Project file: `ElBruno.LocalEmbeddings.csproj`
    - Root namespace: `ElBruno.LocalEmbeddings`
    - Sub-namespaces: `ElBruno.LocalEmbeddings.Extensions`, `ElBruno.LocalEmbeddings.Options`
- Companion packages follow the same rule: `ElBruno.LocalEmbeddings.KernelMemory` (folder, csproj, and namespace).
- Test projects: `ElBruno.LocalEmbeddings.Tests`, `ElBruno.LocalEmbeddings.KernelMemory.Tests`.
- **Never** use `LocalEmbeddings` alone as a folder name, project name, namespace, or PackageId.

## NuGet Package

- **Package ID:** `ElBruno.LocalEmbeddings` (always prefixed with `ElBruno.`)
- **Source:** https://api.nuget.org/v3/index.json
- **Never** use `LocalEmbeddings` alone as the PackageId — it must be `ElBruno.LocalEmbeddings`.

## Code Standards

### C# Coding Conventions
- Follow `.editorconfig` settings for code style
- Use **file-scoped namespaces** (`csharp_style_namespace_declarations = file_scoped`)
- **Organize usings**: System directives first (`dotnet_sort_system_directives_first = true`)
- **var usage**: 
  - Avoid `var` for built-in types
  - Use `var` when type is apparent
  - Prefer `var` for complex types
- **Expression-bodied members**: Use for single-line methods and properties
- **Nullable reference types**: Always enabled - handle null cases properly
- **Implicit usings**: Enabled globally

### Testing Standards
- Use **xUnit** for all unit tests
- Test project naming: `[ProjectName].Tests`
- Use **Moq** for mocking dependencies
- Use **table-driven tests** when testing multiple scenarios
- Test files should mirror the structure of source files
- Use `SkippableFact` attribute for tests that require external dependencies

### Documentation
- Document public APIs with XML comments
- Include usage examples in complex API documentation
- Keep README.md up to date with any API changes
- Extended documentation lives in `docs/` folder

## Project Structure

### Source Projects
- **`src/ElBruno.LocalEmbeddings/`** — Main library implementing `IEmbeddingGenerator<string, Embedding<float>>` using ONNX Runtime
- **`src/ElBruno.LocalEmbeddings.KernelMemory/`** — Companion package providing `ITextEmbeddingGenerator` adapter for Microsoft Kernel Memory integration

### Test Projects
- **`tests/ElBruno.LocalEmbeddings.Tests/`** — Unit tests for main library
- **`tests/ElBruno.LocalEmbeddings.KernelMemory.Tests/`** — Unit tests for Kernel Memory integration

### Samples
- **`samples/ConsoleApp/`** — Basic console application demonstrating library usage
- **`samples/RagChat/`** — RAG (Retrieval-Augmented Generation) chat sample
- **`samples/RagOllama/`** — RAG sample using Ollama LLM
- **`samples/RagFoundryLocal/`** — RAG sample using Azure AI Foundry with local embeddings

## Repository Structure

Keep the root clean. Only these files belong in the repository root:

- `README.md` — Project overview and quick start
- `LICENSE` — MIT license
- `LocalEmbeddings.slnx` — Solution file
- `Directory.Build.props` — Shared build properties
- `.editorconfig` — Code style settings
- `.gitignore` / `.gitattributes` — Git configuration

All other documentation goes in the `docs/` folder:

### Documentation
- `docs/` — Extended documentation (architecture, API reference, contributing guide, etc.)
  - `api-reference.md` — Complete API documentation
  - `configuration.md` — Configuration options and examples
  - `contributing.md` — Contribution guidelines
  - `dependency-injection.md` — DI setup and patterns
  - `getting-started.md` — Step-by-step tutorial
  - `kernel-memory-integration.md` — Kernel Memory integration guide
  - `publishing.md` — Package publishing instructions
  - `squad-workflows.md` — Team workflows and practices
  - `plans/` — Development plans (format: `plan_YYMMDD_HHmm.md`)

### Folder layout

```
├── README.md                  # Keep in root (also packed into NuGet)
├── LICENSE                    # Keep in root
├── LocalEmbeddings.slnx       # Keep in root
├── Directory.Build.props       # Keep in root
├── docs/                       # All extended documentation lives here
│   ├── api-reference.md
│   ├── configuration.md
│   ├── contributing.md
│   ├── kernel-memory-integration.md
│   └── ...
├── src/                        # Source code
│   ├── ElBruno.LocalEmbeddings/
│   └── ElBruno.LocalEmbeddings.KernelMemory/
├── tests/                      # Test projects
│   ├── ElBruno.LocalEmbeddings.Tests/
│   └── ElBruno.LocalEmbeddings.KernelMemory.Tests/
└── samples/                    # Sample applications
    ├── ConsoleApp/
    ├── RagChat/
    ├── RagOllama/
    └── RagFoundryLocal/
```

## Documentation Rules

- **README.md** stays in the root — it is packed into the NuGet package via `<PackageReadmeFile>`.
- Any doc that is **not** the README or LICENSE must go in `docs/`.
- When adding new documentation, create it under `docs/`, not in the root.

## Key Guidelines

1. **Follow .NET best practices** and idiomatic C# patterns
2. **Maintain existing code structure** — don't introduce unnecessary architectural changes
3. **Use dependency injection** patterns where appropriate with `IServiceCollection`
4. **Write unit tests** for new functionality using xUnit
5. **Use table-driven tests** when testing multiple scenarios or edge cases
6. **Document public APIs** with XML comments — include usage examples for complex APIs
7. **Handle nullable reference types** properly — the project has nullable reference types enabled
8. **Follow naming conventions** — all projects, namespaces, and packages must start with `ElBruno.`
9. **Keep the repository root clean** — only essential files in root, extended docs in `docs/`
10. **Update documentation** when making API changes — particularly README.md and relevant docs in `docs/`

## Plans

- All plans are saved in `docs/plans/`.
- Plan files **must** use the naming format: `plan_YYMMDD_HHmm.md` where `YYMMDD` is the 2-digit year, month, day and `HHmm` is the 24-hour time.
- Example: `plan_260212_1933.md` → 2026-02-12 at 19:33.

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
