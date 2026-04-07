// DirectML NPU Worker — runs DirectML benchmark in an isolated process
// to avoid native onnxruntime.dll conflicts with the base ORT package.
// Outputs JSON result to stdout for the parent benchmark to parse.

using System.Diagnostics;
using System.Text.Json;
using ElBruno.LocalEmbeddings.Npu;
using ElBruno.LocalEmbeddings.Npu.Options;
using Microsoft.Extensions.AI;

int textCount = 100;
bool jsonOutput = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--texts" when i + 1 < args.Length:
            if (int.TryParse(args[++i], out int parsed) && parsed > 0)
                textCount = parsed;
            break;
        case "--json":
            jsonOutput = true;
            break;
    }
}

var sampleTexts = GenerateSampleTexts(textCount);

try
{
    var generator = await NpuEmbeddingGenerator.CreateAsync(new NpuEmbeddingsOptions
    {
        PreferQuantized = true,
        AutoDetectNpu = true
    });

    bool npuActive = generator.IsNpuActive;
    string? fallbackReason = generator.FallbackReason;
    string? deviceDescription = generator.DeviceDescription;
    int activeDeviceId = generator.ActiveDeviceId;

    string status;
    if (npuActive)
    {
        status = $"NPU Active (device {activeDeviceId}: {deviceDescription})";
    }
    else
    {
        status = $"DML device {activeDeviceId}";
        if (deviceDescription != null)
            status += $" ({deviceDescription})";
    }

    // Warmup
    await generator.GenerateAsync(["warmup text"]);

    // Timed run
    var sw = Stopwatch.StartNew();
    var embeddings = await generator.GenerateAsync(sampleTexts);
    sw.Stop();

    double totalMs = sw.Elapsed.TotalMilliseconds;
    double textsPerSecond = sampleTexts.Count / sw.Elapsed.TotalSeconds;
    int dimension = embeddings.First().Vector.Length;

    var result = new WorkerResult(
        totalMs, textsPerSecond, dimension,
        npuActive, status, fallbackReason,
        deviceDescription, activeDeviceId, null);

    if (jsonOutput)
    {
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    else
    {
        Console.WriteLine($"DirectML NPU Benchmark — {textCount} texts");
        Console.WriteLine($"  Time:       {totalMs:F1} ms");
        Console.WriteLine($"  Throughput: {textsPerSecond:F1} texts/sec");
        Console.WriteLine($"  Dimension:  {dimension}");
        Console.WriteLine($"  NPU Active: {(npuActive ? "Yes ✅" : "No ❌")}");
        Console.WriteLine($"  Device:     [{activeDeviceId}] {deviceDescription}");
        if (fallbackReason != null)
            Console.WriteLine($"  Fallback:   {fallbackReason}");
    }

    generator.Dispose();
}
catch (Exception ex)
{
    var result = new WorkerResult(0, 0, 0, false, $"Error: {ex.Message}",
        ex.Message, null, -1, ex.ToString());
    if (jsonOutput)
    {
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    else
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}

static IList<string> GenerateSampleTexts(int count)
{
    var topics = new[]
    {
        "The quick brown fox jumps over the lazy dog",
        "Machine learning models can run on neural processing units for faster inference",
        "Local embeddings provide privacy and low latency compared to cloud APIs",
        "Semantic search finds relevant documents by comparing vector similarity",
        "Natural language processing enables computers to understand human text",
        "ONNX Runtime supports multiple hardware accelerators including NPU",
        "Intel Core Ultra processors include an AI Boost neural processing unit",
        "Qualcomm Snapdragon X has a Hexagon Tensor Processor for AI workloads",
        "DirectML provides a unified API for Windows AI hardware acceleration",
        "Sentence transformers convert text into dense vector representations",
        "Retrieval augmented generation improves AI responses with external knowledge",
        "Edge computing brings AI inference closer to the user for real-time processing",
        "INT8 quantization reduces model size while preserving embedding accuracy",
        "The BERT tokenizer splits text into wordpiece subword tokens",
        "Cosine similarity measures the angle between two embedding vectors",
        "Transfer learning allows pre-trained models to work on new tasks",
        "Attention mechanisms help models focus on relevant parts of the input",
        "Transformer architectures revolutionized natural language understanding",
        "Vector databases store and search high-dimensional embedding vectors efficiently",
        "Batch processing multiple texts together improves hardware utilization",
    };

    var texts = new List<string>(count);
    for (int i = 0; i < count; i++)
    {
        var baseText = topics[i % topics.Length];
        texts.Add(i < topics.Length ? baseText : $"{baseText} (variation {i / topics.Length})");
    }

    return texts;
}

record WorkerResult(
    double TotalMs,
    double TextsPerSecond,
    int Dimension,
    bool NpuActive,
    string Status,
    string? FallbackReason,
    string? DeviceDescription,
    int ActiveDeviceId,
    string? Error);
