using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         AOT Cold-Start Measurement Harness                    ║");
Console.WriteLine("║     Measures startup latency for serverless deployments      ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Measurement results
var measurements = new List<ColdStartMeasurement>();

// Process startup timer (from program start)
var processStartTime = Process.GetCurrentProcess().StartTime;
var wallClockStart = Stopwatch.StartNew();

// =============================================================================
// PHASE 1: Model Initialization (Cold Load)
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("PHASE 1: Model Initialization (Cold Load)");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var modelInitStart = Stopwatch.StartNew();
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    EnsureModelDownloaded = true
};

using var generator = await LocalEmbeddingGenerator.CreateAsync(options);
modelInitStart.Stop();

var modelLoadMs = modelInitStart.Elapsed.TotalMilliseconds;
Console.WriteLine($"✓ Model loaded in {modelLoadMs:F2}ms");
Console.WriteLine($"  Provider: {generator.Metadata.ProviderName}");
Console.WriteLine($"  Model: {generator.Metadata.DefaultModelId}");
Console.WriteLine($"  Dimensions: {generator.Metadata.DefaultModelDimensions}");
Console.WriteLine();

// Record first measurement: cold-start to model ready
measurements.Add(new ColdStartMeasurement
{
    Phase = "Model Load (Cold)",
    OperationNumber = 0,
    DurationMs = modelLoadMs,
    CumulativeMs = modelLoadMs,
    Timestamp = DateTime.UtcNow
});

// =============================================================================
// PHASE 2: First Embedding Generation
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("PHASE 2: First Embedding Generation");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var testTexts = new[]
{
    "The quick brown fox jumps over the lazy dog.",
    "Natural language processing is fascinating.",
    "Machine learning models require training data.",
    "Deep neural networks have many layers.",
    "Embeddings capture semantic meaning.",
    "Vector databases enable similarity search.",
    "Tokenization converts text to numbers.",
    "Transformer models revolutionized NLP.",
    "Attention mechanisms improve accuracy.",
    "Transfer learning accelerates model training."
};

double cumulativeMs = modelLoadMs;
var latencies = new List<double>();

for (int i = 0; i < testTexts.Length; i++)
{
    var embeddingStart = Stopwatch.StartNew();
    var embedding = await generator.GenerateAsync(testTexts[i]);
    embeddingStart.Stop();

    var latencyMs = embeddingStart.Elapsed.TotalMilliseconds;
    latencies.Add(latencyMs);
    cumulativeMs += latencyMs;

    var phase = i == 0 ? "First Embedding" : "Steady-State Embedding";
    Console.WriteLine($"[{i + 1:D2}/10] {phase}: {latencyMs:F2}ms (cumulative: {cumulativeMs:F2}ms)");

    measurements.Add(new ColdStartMeasurement
    {
        Phase = phase,
        OperationNumber = i + 1,
        DurationMs = latencyMs,
        CumulativeMs = cumulativeMs,
        Timestamp = DateTime.UtcNow
    });
}

wallClockStart.Stop();
Console.WriteLine();

// =============================================================================
// SUMMARY STATISTICS
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("SUMMARY STATISTICS");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var totalColdStartMs = modelLoadMs + latencies[0]; // Model load + first embedding
var steadyStateLatencies = latencies.Skip(1).ToList();

Console.WriteLine($"Cold-Start Metrics:");
var gateColor = totalColdStartMs > 2000 ? ConsoleColor.Red : ConsoleColor.Green;
Console.ForegroundColor = gateColor;
Console.WriteLine($"  Total cold-start (<2s gate): {totalColdStartMs:F2}ms");
Console.ResetColor();
Console.WriteLine($"  • Model load: {modelLoadMs:F2}ms");
Console.WriteLine($"  • First embedding: {latencies[0]:F2}ms");
Console.WriteLine($"  • Sum: {totalColdStartMs:F2}ms");
Console.WriteLine();

Console.WriteLine($"Steady-State Performance (embeddings 2-10):");
Console.WriteLine($"  Average latency: {steadyStateLatencies.Average():F2}ms");
Console.WriteLine($"  Min latency: {steadyStateLatencies.Min():F2}ms");
Console.WriteLine($"  Max latency: {steadyStateLatencies.Max():F2}ms");
Console.WriteLine($"  Std dev: {CalculateStdDev(steadyStateLatencies):F2}ms");
Console.WriteLine();

Console.WriteLine($"Wall-Clock Time:");
Console.WriteLine($"  Total runtime: {wallClockStart.Elapsed.TotalMilliseconds:F2}ms");
Console.WriteLine();

// Check if cold-start meets the <2s gate
var coldStartOk = totalColdStartMs < 2000;
var gateStatus = coldStartOk ? "✅ PASS" : "❌ FAIL";
Console.WriteLine($"Cold-Start Gate (<2000ms): {gateStatus}");
Console.WriteLine();

// =============================================================================
// SAVE TO CSV
// =============================================================================
var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "cold-start-measurements.csv");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"Saving measurements to: {csvPath}");

using (var writer = new StreamWriter(csvPath))
using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
    csv.WriteRecords(measurements);
}

Console.WriteLine("✓ CSV saved successfully");
Console.WriteLine();

// =============================================================================
// SAVE BASELINE JSON
// =============================================================================
var baselineDir = Path.Combine(
    Path.GetDirectoryName(AppContext.BaseDirectory) ?? ".",
    "..", "..", "..", "..", "tests");

Directory.CreateDirectory(baselineDir);

var baselineFile = Path.Combine(baselineDir, "performance-baseline.json");
var baseline = new
{
    timestamp = DateTime.UtcNow,
    aot_cold_start_ms = (int)totalColdStartMs,
    model_load_ms = (int)modelLoadMs,
    first_embedding_ms = (int)latencies[0],
    steady_state_avg_ms = (int)steadyStateLatencies.Average(),
    steady_state_min_ms = (int)steadyStateLatencies.Min(),
    steady_state_max_ms = (int)steadyStateLatencies.Max(),
    total_for_10_embeddings_ms = (int)cumulativeMs,
    cold_start_gate_ok = coldStartOk
};

var jsonContent = System.Text.Json.JsonSerializer.Serialize(baseline, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(baselineFile, jsonContent);

Console.WriteLine($"Baseline JSON saved to: {baselineFile}");
Console.WriteLine(jsonContent);
Console.WriteLine();

// =============================================================================
// COMPLETION
// =============================================================================
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                 Measurement Complete                           ║");
Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Cold-Start Result: {(coldStartOk ? "✅ PASS" : "❌ FAIL"),-53}║");
Console.WriteLine($"║  Total cold-start: {totalColdStartMs:F0}ms (target: <2000ms) {(coldStartOk ? "" : " [EXCEEDS]")} ║".PadRight(65) + "║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");

// Exit with appropriate code for CI/CD
Environment.Exit(coldStartOk ? 0 : 1);

// =============================================================================
// Helper Methods
// =============================================================================

static double CalculateStdDev(List<double> values)
{
    if (values.Count < 2) return 0;

    var avg = values.Average();
    var sumSquaredDiff = values.Sum(x => Math.Pow(x - avg, 2));
    return Math.Sqrt(sumSquaredDiff / (values.Count - 1));
}

// =============================================================================
// Data Classes
// =============================================================================

public class ColdStartMeasurement
{
    public string Phase { get; set; } = string.Empty;
    public int OperationNumber { get; set; }
    public double DurationMs { get; set; }
    public double CumulativeMs { get; set; }
    public DateTime Timestamp { get; set; }
}
