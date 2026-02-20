# VisionMemoryAgentSample

A minimal console app that combines **CLIP image embeddings** (local, ONNX) with an **Ollama LLM agent** that can ingest images and search them using natural language — all running locally, no cloud services.

## What It Does

1. Loads CLIP models (text + vision encoders) for local embedding generation.
2. Connects to a local Ollama LLM with tool-calling support.
3. Exposes two tools to the agent:
   - **IngestImage(path, tagsCsv)** — generates a CLIP embedding for an image and stores it in memory.
   - **FindSimilarImages(query, topK)** — encodes a text query with CLIP and finds the most similar images by cosine similarity.
4. Runs an interactive chat loop where the LLM decides when to call each tool.

Everything runs locally. No databases, no cloud APIs, no persistence — pure in-memory.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com/) installed and running locally
- A tool-calling capable model pulled in Ollama (e.g., `ollama pull llama3.2`)
- CLIP ONNX models (text_model.onnx, vision_model.onnx, vocab.json, merges.txt)

### Getting CLIP Models

```bash
pip install optimum[exporters]
optimum-cli export onnx --model openai/clip-vit-base-patch32 ./clip-models/
```

## How to Run

```bash
# Make sure Ollama is running
ollama serve

# From the repository root
dotnet run --project samples/VisionMemoryAgentSample -- --model-dir ./clip-models
```

Optional: specify a different Ollama model:

```bash
dotnet run --project samples/VisionMemoryAgentSample -- --model-dir ./clip-models --ollama-model llama3.2
```

## Example Usage

```
Vision Memory Agent ready! Type a message (or 'exit' to quit).

> Please ingest the image at ./photos/cat.jpg with tags cat,pet,animal

Ingested 'cat.jpg' with tags [cat,pet,animal]. Store now has 1 image(s).

> Ingest ./photos/sunset.jpg with tags sunset,nature,sky

Ingested 'sunset.jpg' with tags [sunset,nature,sky]. Store now has 2 image(s).

> Find images similar to "a beautiful sky at dusk"

Top 1 result(s):
  1. sunset.jpg (score: 0.2845, tags: sunset,nature,sky)

> exit
Goodbye!
```
