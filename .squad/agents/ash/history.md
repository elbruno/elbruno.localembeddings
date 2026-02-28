# Ash — History & Learnings

## Project Context

- **Project:** ElBruno.LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI and ONNX Runtime
- **Owner:** Bruno Capuano
- **Stack:** .NET 8.0 / 10.0 (multi-target), C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models, NuGet package distribution
- **Joined:** 2026-02-28

## Key Security Concerns for This Project

1. **Model downloads:** The library downloads ONNX model files from HuggingFace/CDNs over HTTPS. Integrity verification (hash/checksum) of downloaded models is a priority concern.
2. **File path handling:** Model cache paths are constructed from user-provided options — path traversal risk if inputs are not sanitized.
3. **NuGet dependencies:** SixLabors.ImageSharp had known vulnerabilities in 3.1.6 and 3.1.7 (CVE-level advisories). The team upgraded to 3.1.12. Ongoing advisory monitoring needed.
4. **Public NuGet package:** As a published library, security issues affect downstream consumers.

## Learnings

<!-- Append new learnings here as work progresses -->
