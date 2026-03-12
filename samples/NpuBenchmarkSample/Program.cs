using System.Diagnostics;
using Microsoft.Extensions.AI;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using ElBruno.LocalEmbeddings.Npu;
using ElBruno.LocalEmbeddings.Npu.Options;
using ElBruno.LocalEmbeddings.Npu.Qualcomm;
using ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;
using ElBruno.LocalEmbeddings.Npu.Intel;
using ElBruno.LocalEmbeddings.Npu.Intel.Options;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          NPU Embedding Benchmark — ElBruno.LocalEmbeddings  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// --- Enumerate DXGI adapters to show available hardware ---
var adapters = DxgiDeviceHelper.EnumerateAdapters();
if (adapters.Count > 0)
{
    Console.WriteLine("🔍 Detected DXGI adapters:");
    foreach (var adapter in adapters)
    {
        string tag = adapter.IsLikelyNpu ? " ← NPU detected!" : "";
        string mem = adapter.DedicatedVideoMemoryBytes > 0
            ? $" ({adapter.DedicatedVideoMemoryBytes / (1024 * 1024)} MB)"
            : " (shared memory)";
        Console.WriteLine($"   [{adapter.Index}] {adapter.Description}{mem}{tag}");
    }
    Console.WriteLine();

    int? npuIndex = DxgiDeviceHelper.FindNpuDeviceIndex();
    if (npuIndex.HasValue)
    {
        Console.WriteLine($"✅ NPU found at device index {npuIndex.Value} — DirectML will target it automatically.");
    }
    else
    {
        Console.WriteLine("⚠️  No NPU adapter detected. DirectML will use device 0 (likely GPU).");
    }
    Console.WriteLine();
}
else
{
    Console.WriteLine("⚠️  Could not enumerate DXGI adapters (non-Windows or DXGI unavailable).\n");
}

// --- Select provider ---
Console.WriteLine("Select execution provider:");
Console.WriteLine("  [1] CPU (baseline)");
Console.WriteLine("  [2] DirectML NPU (Windows generic)");
Console.WriteLine("  [3] Qualcomm QNN NPU (Snapdragon X)");
Console.WriteLine("  [4] Intel OpenVINO NPU (Core Ultra)");
Console.WriteLine("  [5] Run ALL and compare");
Console.WriteLine();
Console.Write("Choice [1-5] (default: 5): ");
var choice = Console.ReadLine()?.Trim();
if (string.IsNullOrEmpty(choice)) choice = "5";

// --- How many texts ---
Console.Write("Number of texts to embed (default: 100): ");
var countInput = Console.ReadLine()?.Trim();
int textCount = int.TryParse(countInput, out var parsed) && parsed > 0 ? parsed : 100;

// Generate sample texts
var sampleTexts = GenerateSampleTexts(textCount);
Console.WriteLine($"\n📝 Generated {sampleTexts.Count} sample texts\n");

if (choice == "5")
{
    // Run all providers and compare
    var results = new List<BenchmarkResult>();

    results.Add(await RunCpuBenchmark(sampleTexts));
    results.Add(await RunDirectMLBenchmark(sampleTexts));
    results.Add(await RunQualcommBenchmark(sampleTexts));
    results.Add(await RunIntelBenchmark(sampleTexts));

    PrintComparisonTable(results, textCount);
}
else
{
    BenchmarkResult result = choice switch
    {
        "1" => await RunCpuBenchmark(sampleTexts),
        "2" => await RunDirectMLBenchmark(sampleTexts),
        "3" => await RunQualcommBenchmark(sampleTexts),
        "4" => await RunIntelBenchmark(sampleTexts),
        _ => await RunCpuBenchmark(sampleTexts)
    };

    PrintSingleResult(result, textCount);
}

Console.WriteLine("\nDone! Press any key to exit.");
Console.ReadKey();

// ────────────────────────────────────────────────────────────
// Benchmark runners
// ────────────────────────────────────────────────────────────

static async Task<BenchmarkResult> RunCpuBenchmark(IList<string> texts)
{
    Console.Write("⏳ CPU baseline... ");
    try
    {
        var generator = await LocalEmbeddingGenerator.CreateAsync(new LocalEmbeddingsOptions
        {
            PreferQuantized = true
        });
        return await RunBenchmark("CPU", generator, texts, npuActive: false, npuStatus: "N/A");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {ex.Message}");
        return new BenchmarkResult("CPU", 0, 0, 0, false, $"Error: {ex.Message}");
    }
}

static async Task<BenchmarkResult> RunDirectMLBenchmark(IList<string> texts)
{
    Console.Write("⏳ DirectML NPU... ");
    try
    {
        var generator = await NpuEmbeddingGenerator.CreateAsync(new NpuEmbeddingsOptions
        {
            PreferQuantized = true,
            AutoDetectNpu = true
        });

        bool npuActive = generator.IsNpuActive;
        string status;
        if (npuActive)
        {
            status = $"NPU Active (device {generator.ActiveDeviceId}: {generator.DeviceDescription})";
        }
        else
        {
            status = $"DML device {generator.ActiveDeviceId}";
            if (generator.DeviceDescription != null)
                status += $" ({generator.DeviceDescription})";
            if (generator.FallbackReason != null)
            {
                Console.WriteLine($"⚠️  {generator.FallbackReason}");
                Console.Write("    ");
            }
        }

        return await RunBenchmark("DirectML", generator, texts, npuActive, status);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {ex.Message}");
        return new BenchmarkResult("DirectML", 0, 0, 0, false, $"Error: {ex.Message}");
    }
}

static async Task<BenchmarkResult> RunQualcommBenchmark(IList<string> texts)
{
    Console.Write("⏳ Qualcomm QNN... ");
    try
    {
        var generator = await QualcommEmbeddingGenerator.CreateAsync(new QualcommEmbeddingsOptions
        {
            PreferQuantized = true,
            FallbackToCpu = true
        });
        bool qnnActive = generator.IsQnnActive;
        string status = qnnActive ? "QNN HTP Active" : $"CPU Fallback";
        if (!qnnActive && generator.FallbackReason != null)
        {
            Console.WriteLine($"⚠️  QNN unavailable: {generator.FallbackReason}");
            Console.Write("    ");
        }
        return await RunBenchmark("Qualcomm QNN", generator, texts, qnnActive, status);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {ex.Message}");
        return new BenchmarkResult("Qualcomm QNN", 0, 0, 0, false, $"Error: {ex.Message}");
    }
}

static async Task<BenchmarkResult> RunIntelBenchmark(IList<string> texts)
{
    Console.Write("⏳ Intel OpenVINO... ");
    try
    {
        var generator = await IntelEmbeddingGenerator.CreateAsync(new IntelEmbeddingsOptions
        {
            PreferQuantized = true,
            FallbackToCpu = true
        });
        bool ovinoActive = generator.IsOpenVinoActive;
        string status = ovinoActive ? "OpenVINO NPU Active" : $"CPU Fallback";
        if (!ovinoActive && generator.FallbackReason != null)
        {
            Console.WriteLine($"⚠️  OpenVINO unavailable: {generator.FallbackReason}");
            Console.Write("    ");
        }
        return await RunBenchmark("Intel OpenVINO", generator, texts, ovinoActive, status);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {ex.Message}");
        return new BenchmarkResult("Intel OpenVINO", 0, 0, 0, false, $"Error: {ex.Message}");
    }
}

static async Task<BenchmarkResult> RunBenchmark(
    string name,
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IList<string> texts,
    bool npuActive,
    string npuStatus)
{
    using var gen = generator as IDisposable;

    // Warmup with a single embedding
    await generator.GenerateAsync(["warmup text"]);

    // Timed run
    var sw = Stopwatch.StartNew();
    var embeddings = await generator.GenerateAsync(texts);
    sw.Stop();

    double totalMs = sw.Elapsed.TotalMilliseconds;
    double textsPerSecond = texts.Count / sw.Elapsed.TotalSeconds;
    int dimension = embeddings.First().Vector.Length;

    Console.WriteLine($"✅ {totalMs:F1}ms ({textsPerSecond:F1} texts/sec, dim={dimension})");

    return new BenchmarkResult(name, totalMs, textsPerSecond, dimension, npuActive, npuStatus);
}

// ────────────────────────────────────────────────────────────
// Output formatting
// ────────────────────────────────────────────────────────────

static void PrintComparisonTable(List<BenchmarkResult> results, int textCount)
{
    Console.WriteLine($"\n{"",2}╔══════════════════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"{"",2}║  NPU Benchmark Results — {textCount} texts                                               ║");
    Console.WriteLine($"{"",2}╠══════════════════╤══════════╤══════════════╤═════╤══════╤════════════════════════╣");
    Console.WriteLine($"{"",2}║ Provider         │ Time(ms) │ Texts/sec    │ Dim │ NPU? │ Status                 ║");
    Console.WriteLine($"{"",2}╠══════════════════╪══════════╪══════════════╪═════╪══════╪════════════════════════╣");

    foreach (var r in results)
    {
        string npu = r.NpuActive ? " ✅ " : " ❌ ";
        if (r.TotalMs == 0)
        {
            Console.WriteLine($"{"",2}║ {r.Name,-16} │ {"—",-8} │ {"—",-12} │ {"—",-3} │{npu}│ {r.NpuStatus,-22} ║");
        }
        else
        {
            Console.WriteLine($"{"",2}║ {r.Name,-16} │ {r.TotalMs,8:F1} │ {r.TextsPerSecond,12:F1} │ {r.Dimension,3} │{npu}│ {r.NpuStatus,-22} ║");
        }
    }

    Console.WriteLine($"{"",2}╚══════════════════╧══════════╧══════════════╧═════╧══════╧════════════════════════╝");

    // Find fastest
    var successful = results.Where(r => r.TotalMs > 0).ToList();
    if (successful.Count > 1)
    {
        var fastest = successful.OrderBy(r => r.TotalMs).First();
        var baseline = successful.First();
        if (fastest.Name != baseline.Name && baseline.TotalMs > 0)
        {
            double speedup = baseline.TotalMs / fastest.TotalMs;
            Console.WriteLine($"\n  🏆 Fastest: {fastest.Name} ({speedup:F2}x faster than {baseline.Name})");
        }
        else
        {
            Console.WriteLine($"\n  🏆 Fastest: {fastest.Name}");
        }
    }
}

static void PrintSingleResult(BenchmarkResult result, int textCount)
{
    Console.WriteLine($"\n┌─────────────────────────────────────┐");
    Console.WriteLine($"│  Result: {result.Name,-27}│");
    Console.WriteLine($"├─────────────────────────────────────┤");
    Console.WriteLine($"│  Texts:          {textCount,-19}│");
    Console.WriteLine($"│  Total time:     {result.TotalMs:F1} ms{"",-13}│");
    Console.WriteLine($"│  Throughput:     {result.TextsPerSecond:F1} texts/sec{"",-7}│");
    Console.WriteLine($"│  Dimension:      {result.Dimension,-19}│");
    Console.WriteLine($"│  NPU Active:     {(result.NpuActive ? "Yes ✅" : "No ❌"),-19}│");
    Console.WriteLine($"│  Status:         {result.NpuStatus,-19}│");
    Console.WriteLine($"└─────────────────────────────────────┘");
}

// ────────────────────────────────────────────────────────────
// Sample data generation
// ────────────────────────────────────────────────────────────

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
        // Add variation to each text to produce distinct embeddings
        texts.Add(i < topics.Length ? baseText : $"{baseText} (variation {i / topics.Length})");
    }

    return texts;
}

// ────────────────────────────────────────────────────────────
// Result record
// ────────────────────────────────────────────────────────────

record BenchmarkResult(
    string Name,
    double TotalMs,
    double TextsPerSecond,
    int Dimension,
    bool NpuActive,
    string NpuStatus);
