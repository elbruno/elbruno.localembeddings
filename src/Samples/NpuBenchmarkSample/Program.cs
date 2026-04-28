using System.Diagnostics;
using System.Management;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using ElBruno.LocalEmbeddings.Npu.Qualcomm;
using ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          NPU Embedding Benchmark — ElBruno.LocalEmbeddings  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// --- Detect NPU hardware via WMI (ComputeAccelerator class) ---
// Intel NPUs do NOT appear as DXGI adapters — they are PCI ComputeAccelerator devices.
bool hasIntelNpuHardware = false;
if (OperatingSystem.IsWindows())
{
    try
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, Manufacturer, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass = 'ComputeAccelerator'");
        var npuDevices = searcher.Get().Cast<ManagementObject>().ToList();
        if (npuDevices.Count > 0)
        {
            Console.WriteLine("🧠 NPU hardware detected (ComputeAccelerator devices):");
            foreach (var dev in npuDevices)
            {
                string name = dev["Name"]?.ToString() ?? "Unknown";
                string mfg = dev["Manufacturer"]?.ToString() ?? "";
                string pnpId = dev["PNPDeviceID"]?.ToString() ?? "";
                Console.WriteLine($"   • {name} ({mfg})");
                Console.WriteLine($"     PnP: {pnpId}");
                if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                    pnpId.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase))
                {
                    hasIntelNpuHardware = true;
                }
            }
            Console.WriteLine();
            if (hasIntelNpuHardware)
            {
                Console.WriteLine("ℹ️  Intel NPU detected — OpenVINO & DirectML benchmarks run in separate processes");
                Console.WriteLine("   (Each ORT variant ships its own onnxruntime.dll — only one can load per process)");
                Console.WriteLine();
            }
        }
    }
    catch
    {
        // WMI may not be available in all environments
    }
}

Console.WriteLine("ℹ️  DirectML and Intel OpenVINO benchmarks run in isolated worker processes");
Console.WriteLine("   (each ORT variant ships its own native DLL — only one can load per process)");
Console.WriteLine();

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
        // DirectML uses Microsoft.ML.OnnxRuntime.DirectML which ships its own
        // onnxruntime.dll (with DML entry points). This conflicts with the base
        // Microsoft.ML.OnnxRuntime DLL. Run in a separate process to isolate.
        string workerDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DirectMLNpuWorker"));
        if (!Directory.Exists(workerDir))
        {
            Console.WriteLine("⚠️  DirectMLNpuWorker project not found");
            return new BenchmarkResult("DirectML", 0, 0, 0, false,
                $"Worker not found at {workerDir}");
        }

        // Build the worker project first
        var buildPsi = new ProcessStartInfo("dotnet", $"build \"{workerDir}\" -c Release --nologo -v q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var buildProc = Process.Start(buildPsi)!;
        await buildProc.WaitForExitAsync();
        if (buildProc.ExitCode != 0)
        {
            string buildErr = await buildProc.StandardError.ReadToEndAsync();
            Console.WriteLine($"⚠️  Worker build failed");
            return new BenchmarkResult("DirectML", 0, 0, 0, false,
                $"Build failed: {buildErr.Split('\n').FirstOrDefault()?.Trim()}");
        }

        // Run the worker with --json flag
        var runPsi = new ProcessStartInfo("dotnet",
            $"run --project \"{workerDir}\" -c Release --no-build -- --texts {texts.Count} --json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var runProc = Process.Start(runPsi)!;
        string stdout = await runProc.StandardOutput.ReadToEndAsync();
        await runProc.WaitForExitAsync();

        // Parse the JSON output (last non-empty line)
        string? jsonLine = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();

        if (string.IsNullOrEmpty(jsonLine))
        {
            string stderr = await runProc.StandardError.ReadToEndAsync();
            Console.WriteLine($"⚠️  No output from worker");
            return new BenchmarkResult("DirectML", 0, 0, 0, false,
                $"No output: {stderr.Split('\n').FirstOrDefault()?.Trim()}");
        }

        using var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        double totalMs = root.GetProperty("TotalMs").GetDouble();
        double textsPerSecond = root.GetProperty("TextsPerSecond").GetDouble();
        int dimension = root.GetProperty("Dimension").GetInt32();
        bool npuActive = root.GetProperty("NpuActive").GetBoolean();
        string status = root.GetProperty("Status").GetString() ?? "Unknown";

        if (root.TryGetProperty("Error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
        {
            string? error = errProp.GetString();
            if (error != null)
            {
                Console.WriteLine($"⚠️  {error}");
                Console.Write("    ");
            }
        }

        if (!npuActive && root.TryGetProperty("FallbackReason", out var fbProp)
            && fbProp.ValueKind == JsonValueKind.String)
        {
            string? reason = fbProp.GetString();
            if (reason != null)
            {
                Console.WriteLine($"⚠️  DirectML: {reason}");
                Console.Write("    ");
            }
        }

        Console.WriteLine($"✅ {totalMs:F1}ms ({textsPerSecond:F1} texts/sec, dim={dimension})");
        return new BenchmarkResult("DirectML", totalMs, textsPerSecond, dimension,
            npuActive, status + " [isolated process]");
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
        // Intel OpenVINO uses ORT 1.21.0 which conflicts with DirectML's ORT 1.24.x.
        // Run in a separate process (IntelNpuWorker) so each loads its own ORT version.
        string workerDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "IntelNpuWorker"));
        if (!Directory.Exists(workerDir))
        {
            Console.WriteLine("⚠️  IntelNpuWorker project not found");
            return new BenchmarkResult("Intel OpenVINO", 0, 0, 0, false,
                $"Worker not found at {workerDir}");
        }

        // Build the worker project first
        var buildPsi = new ProcessStartInfo("dotnet", $"build \"{workerDir}\" -c Release --nologo -v q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var buildProc = Process.Start(buildPsi)!;
        await buildProc.WaitForExitAsync();
        if (buildProc.ExitCode != 0)
        {
            string buildErr = await buildProc.StandardError.ReadToEndAsync();
            Console.WriteLine($"⚠️  Worker build failed");
            return new BenchmarkResult("Intel OpenVINO", 0, 0, 0, false,
                $"Build failed: {buildErr.Split('\n').FirstOrDefault()?.Trim()}");
        }

        // Run the worker with --json flag
        var runPsi = new ProcessStartInfo("dotnet",
            $"run --project \"{workerDir}\" -c Release --no-build -- --texts {texts.Count} --json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var runProc = Process.Start(runPsi)!;
        string stdout = await runProc.StandardOutput.ReadToEndAsync();
        await runProc.WaitForExitAsync();

        // Parse the JSON output (last non-empty line)
        string? jsonLine = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();

        if (string.IsNullOrEmpty(jsonLine))
        {
            string stderr = await runProc.StandardError.ReadToEndAsync();
            Console.WriteLine($"⚠️  No output from worker");
            return new BenchmarkResult("Intel OpenVINO", 0, 0, 0, false,
                $"No output: {stderr.Split('\n').FirstOrDefault()?.Trim()}");
        }

        using var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        double totalMs = root.GetProperty("TotalMs").GetDouble();
        double textsPerSecond = root.GetProperty("TextsPerSecond").GetDouble();
        int dimension = root.GetProperty("Dimension").GetInt32();
        bool npuActive = root.GetProperty("NpuActive").GetBoolean();
        string status = root.GetProperty("Status").GetString() ?? "Unknown";

        if (root.TryGetProperty("Error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
        {
            string? error = errProp.GetString();
            if (error != null)
            {
                Console.WriteLine($"⚠️  {error}");
                Console.Write("    ");
            }
        }

        if (!npuActive && root.TryGetProperty("FallbackReason", out var fbProp)
            && fbProp.ValueKind == JsonValueKind.String)
        {
            string? reason = fbProp.GetString();
            if (reason != null)
            {
                Console.WriteLine($"⚠️  OpenVINO: {reason}");
                Console.Write("    ");
            }
        }

        Console.WriteLine($"✅ {totalMs:F1}ms ({textsPerSecond:F1} texts/sec, dim={dimension})");
        return new BenchmarkResult("Intel OpenVINO", totalMs, textsPerSecond, dimension,
            npuActive, status + " [isolated process]");
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
