# Copilot Instructions — ElBruno.LocalEmbeddings

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

## Project

- .NET library for local embedding generation using `Microsoft.Extensions.AI` and ONNX Runtime.
- **Repository:** https://github.com/elbruno/elbruno.localembeddings
- Main project: `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj`
- Kernel Memory companion: `src/ElBruno.LocalEmbeddings.KernelMemory/ElBruno.LocalEmbeddings.KernelMemory.csproj`
- Tests: `tests/ElBruno.LocalEmbeddings.Tests/` and `tests/ElBruno.LocalEmbeddings.KernelMemory.Tests/`
- Samples: `samples/ConsoleApp/`, `samples/RagChat/`, `samples/RagOllama/`, `samples/RagFoundryLocal/`

## Repository Structure

Keep the root clean. Only these files belong in the repository root:

- `README.md` — Project overview and quick start
- `LICENSE` — MIT license
- `LocalEmbeddings.slnx` — Solution file
- `Directory.Build.props` — Shared build properties
- `.editorconfig` — Code style settings
- `.gitignore` / `.gitattributes` — Git configuration

All other documentation goes in the `docs/` folder:

- `docs/` — Extended documentation (architecture, API reference, contributing guide, etc.)

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

## Plans

- All plans are saved in `docs/plans/`.
- Plan files **must** use the naming format: `plan_YYMMDD_HHmm.md` where `YYMMDD` is the 2-digit year, month, day and `HHmm` is the 24-hour time.
- Example: `plan_260212_1933.md` → 2026-02-12 at 19:33.

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
