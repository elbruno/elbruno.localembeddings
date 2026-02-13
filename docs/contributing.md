# Contributing — ElBruno.LocalEmbeddings

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- Git

## Getting Started

```bash
git clone https://github.com/elbruno/elbruno.localembeddings.git
cd elbruno.localembeddings
dotnet build
```

## Running Tests

```bash
dotnet test
```

## Repository Structure

```
├── README.md                  # Project overview (packed into NuGet)
├── LICENSE                    # MIT license
├── LocalEmbeddings.slnx       # Solution file
├── Directory.Build.props       # Shared build properties
├── docs/                       # Extended documentation
├── src/
│   ├── ElBruno.LocalEmbeddings/               # Core library (M.E.AI + ONNX)
│   └── ElBruno.LocalEmbeddings.KernelMemory/   # Kernel Memory companion package
├── tests/
│   ├── ElBruno.LocalEmbeddings.Tests/                # Core unit tests
│   └── ElBruno.LocalEmbeddings.KernelMemory.Tests/   # KM adapter tests
└── samples/                    # Sample applications
    ├── ConsoleApp/
    ├── RagChat/
    ├── RagOllama/
    └── RagFoundryLocal/
```

## Guidelines

- Keep the root directory clean — only README, LICENSE, solution, and build config files belong there.
- All extended documentation goes in `docs/`.
- The NuGet package IDs are always prefixed with `ElBruno.` (e.g., `ElBruno.LocalEmbeddings`, `ElBruno.LocalEmbeddings.KernelMemory`).
- The core `ElBruno.LocalEmbeddings` package must **not** depend on Kernel Memory — KM integration lives in the companion package.
- Target .NET 10.0 or later.

## License

This project is licensed under the MIT License — see the [LICENSE](../LICENSE) file for details.
