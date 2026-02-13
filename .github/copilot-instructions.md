# Copilot Instructions — elbruno.LocalEmbeddings

## NuGet Package

- **Package ID:** `elbruno.LocalEmbeddings` (always prefixed with `elbruno.`)
- **Source:** https://api.nuget.org/v3/index.json
- **Never** use `LocalEmbeddings` alone as the PackageId — it must be `elbruno.LocalEmbeddings`.

## Project

- .NET library for local embedding generation using `Microsoft.Extensions.AI` and ONNX Runtime.
- **Repository:** https://github.com/elbruno/elbruno.localembeddings
- Main project: `src/LocalEmbeddings/LocalEmbeddings.csproj`
- Tests: `tests/LocalEmbeddings.Tests/`
- Samples: `samples/ConsoleApp/` and `samples/RagChat/`

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
│   └── contributing.md
├── src/                        # Source code
│   └── LocalEmbeddings/
├── tests/                      # Test projects
│   └── LocalEmbeddings.Tests/
└── samples/                    # Sample applications
    ├── ConsoleApp/
    └── RagChat/
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
