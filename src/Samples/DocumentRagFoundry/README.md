# DocumentRagFoundry — Multimodal Document Analysis

An interactive RAG (Retrieval-Augmented Generation) sample that combines PDF processing, image embeddings (CLIP), text embeddings, and Foundry Local LLM (phi-4-mini) for intelligent multimodal document analysis.

## Features

- **PDF Processing:** Extract text and convert pages to images
- **Dual Embeddings:** Text embeddings + CLIP image embeddings for multimodal search
- **Standalone Images:** Index and search standalone images alongside PDFs
- **Foundry Local:** Uses phi-4-mini running locally for answer generation
- **Interactive UI:** Rich console interface with Spectre.Console
- **Context-Aware Answers:** LLM generates answers using retrieved text snippets and images

## Prerequisites

1. **Foundry Local** must be running with **phi-4-mini** model available
2. **CLIP models** downloaded (for image embeddings):

   ```powershell
   ./scripts/download_clip_models.ps1
   ```

## Setup

### 1. Add Your Documents

Place your documents in the `samples/docs/` folder:

```
samples/docs/
├── document1.pdf
├── document2.pdf
├── image1.jpg
└── image2.jpg
```

**Supported formats:**

- PDFs: `.pdf`
- Images: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`

### 2. Run the Application

**Default (models in `./scripts/clip-models`):**

```bash
dotnet run --project samples/DocumentRagFoundry
```

**Custom model directory:**

```bash
dotnet run --project samples/DocumentRagFoundry -- /path/to/clip-models
```

## How It Works

### 1. Indexing Phase

- **PDFs:**
  - Extracts text content from each page
  - Converts each page to an image
  - Generates text embeddings for content
  - Generates CLIP image embeddings for page images

- **Standalone Images:**
  - Generates CLIP image embeddings
  - Indexed alongside PDF images

### 2. Query Phase

- User asks a question in natural language
- System retrieves top-K relevant text segments (using text embeddings)
- System retrieves top-M relevant images (using CLIP multimodal capabilities)
- Results are combined and ranked by similarity

### 3. Answer Generation

- Retrieved context (text + images) is formatted into a prompt
- Foundry Local phi-4-mini generates a streaming answer
- Answer acknowledges sources and page numbers

## Example Session

```
Question: What are the main topics in the documents?

Retrieved Context:
┌──────┬────────────────┬──────┬──────────────────────────────┬───────┐
│ Type │ Source         │ Page │ Preview/Path                 │ Score │
├──────┼────────────────┼──────┼──────────────────────────────┼───────┤
│ Text │ document1.pdf  │ 1    │ Introduction to machine...   │ 0.892 │
│ Text │ document2.pdf  │ 3    │ Deep learning architectures  │ 0.854 │
│ Image│ document1.pdf  │ 2    │ document1_page_2.png         │ 0.789 │
└──────┴────────────────┴──────┴──────────────────────────────┴───────┘

Answer:
════════════════════════════════════════════════════════════════════════
Based on the retrieved context, the main topics are:

1. Machine learning fundamentals (document1.pdf, page 1)
2. Deep learning architectures (document2.pdf, page 3)

The documents appear to cover technical AI/ML content with supporting diagrams.
════════════════════════════════════════════════════════════════════════
```

## Architecture

### Components

```
User Query → Text Embedding → Retrieval (Text + Images) → Foundry Local → Answer
```

### Service Classes

- **PdfProcessor:** Extracts text and converts PDF pages to images
  - Note: Uses PdfSharp for structure; placeholder implementations for text extraction and rendering
  - For production, consider PdfPig (text) and PDFium.NET (rendering)

- **MultimodalDocumentIndex:** Manages dual embedding indexes
  - Text segments → LocalEmbeddings
  - Images → CLIP embeddings
  - Hybrid retrieval combining both

- **DocumentQaGenerator:** Foundry Local integration
  - Manages `FoundryLocalManager` lifecycle
  - Formats prompts with retrieved context
  - Streams answers from phi-4-mini

## Commands

- Type any question to search and get an answer
- Type `exit` to quit

## Technical Notes

### PDF Text Extraction

The current implementation uses **PdfSharp** which doesn't include native text extraction. The `PdfProcessor` includes placeholder logic.

**For production use, consider:**

- **PdfPig:** Excellent text extraction library
- **iTextSharp:** Commercial option with comprehensive features
- **OCR:** For scanned PDFs (Tesseract, Azure AI Document Intelligence)

### PDF Rendering

Current implementation creates placeholder images.

**For production use, consider:**

- **PDFium.NET:** High-quality PDF rendering
- **SkiaSharp + PDFium:** Cross-platform rendering
- **Windows.Data.Pdf:** Windows-specific API

## Configuration

The application uses these defaults:

- **Docs folder:** `../docs` (relative to sample project)
- **Temp images:** `./temp_pdf_images` (auto-cleaned on exit)
- **Model:** `phi-4-mini` (Foundry Local)
- **Top-K results:** 5 (text) + 2-3 (images)

## Troubleshooting

**"Foundry Local connection failed"**

- Ensure Foundry Local is running
- Verify phi-4-mini model is available

**"No documents found"**

- Check that PDFs/images are in `samples/docs/`
- Verify file permissions

**"CLIP model not found"**

- Run the CLIP model download script:

  ```powershell
  ./scripts/download_clip_models.ps1
  ```

## Future Enhancements

- Replace PdfSharp text extraction with PdfPig
- Add PDFium.NET for proper PDF rendering
- Support for more document formats (DOCX, TXT, HTML)
- Image-to-image search mode
- Export results to markdown/HTML
- Configurable retrieval strategies

## Learn More

- [LocalEmbeddings Documentation](../../docs/)
- [Image Embeddings Guide](../README_IMAGES.md)
- [RagFoundryLocal Sample](../RagFoundryLocal/)
