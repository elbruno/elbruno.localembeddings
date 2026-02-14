### ModelDownloader Design Decisions

**Date:** 2026-02-12  
**Author:** Dallas (Core Dev)

#### Cache Path Strategy
- Windows uses `%LOCALAPPDATA%\LocalEmbeddings\models\`
- Linux/macOS uses XDG_DATA_HOME (defaulting to `~/.local/share/LocalEmbeddings/models/`)
- Model names are sanitized (slashes → underscores) for path safety

#### HuggingFace Download URLs
- ONNX model: `https://huggingface.co/{model}/resolve/main/onnx/model.onnx`
- Tokenizer files: `https://huggingface.co/{model}/resolve/main/{file}`
- The `/onnx/model.onnx` path is standard for sentence-transformers models

#### Caching Behavior
- Simple existence check (no hash verification currently)
- Uses `.tmp` files during download to prevent partial file corruption
- Tokenizer files are optional — missing files don't fail the download

#### Interface Added
- `IModelDownloader` interface enables DI and unit testing
- Both interface and class are public for direct usage
