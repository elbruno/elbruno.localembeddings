# Harrier Code Fixes

## Summary
- Hardened Harrier tokenizer to apply SentencePiece normalization, enforce minimum maxLength, and guard tokenizer.json size.
- Serialized Harrier model downloads, verified sidecar hashes (including .onnx_data), and reduced redundant hashing.
- Added Linux ONNX alias support and diagnostics, fixed provider naming, updated HttpClient handler, and added explicit package references.

## Rationale
- Align Harrier behavior with base library reliability, security, and performance patterns.
- Prevent cache corruption, missing data file failures, and platform-specific ONNX Runtime load issues.
