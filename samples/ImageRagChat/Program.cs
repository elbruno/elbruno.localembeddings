using ElBruno.LocalEmbeddings.ImageEmbeddings;
using ImageRagChat.ConsoleUi;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

ImageRagChatConsoleRenderer.PrintBanner();

// =============================================================================
// Step 1: Parse Arguments
// =============================================================================
if (args.Length < 2)
{
    AnsiConsole.MarkupLine("[bold red]Usage:[/] ImageRagChat [green]<model-directory>[/] [green]<image-directory>[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Arguments:");
    AnsiConsole.MarkupLine("  [green]model-directory[/]   Directory containing CLIP ONNX models:");
    AnsiConsole.MarkupLine("                       text_model.onnx, vision_model.onnx, vocab.json, merges.txt");
    AnsiConsole.MarkupLine("  [green]image-directory[/]   Directory containing images to search");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("Example:");
    AnsiConsole.MarkupLine("  [grey]dotnet run --project samples/ImageRagChat -- ./clip-models ./my-images[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("To get CLIP models:");
    AnsiConsole.MarkupLine("  [grey]pip install optimum[exporters][/]");
    AnsiConsole.MarkupLine("  [grey]optimum-cli export onnx --model openai/clip-vit-base-patch32 ./clip-models/[/]");
    return;
}

string modelDir = args[0];
string imageDir = args[1];

if (!Directory.Exists(modelDir))
{
    ImageRagChatConsoleRenderer.PrintError($"Model directory not found: {modelDir}");
    return;
}

if (!Directory.Exists(imageDir))
{
    ImageRagChatConsoleRenderer.PrintError($"Image directory not found: {imageDir}");
    return;
}

// =============================================================================
// Step 2: Load CLIP Models
// =============================================================================
ImageRagChatConsoleRenderer.PrintStepHeader("Step 1: Loading CLIP models");

string textModelPath = Path.Combine(modelDir, "text_model.onnx");
string visionModelPath = Path.Combine(modelDir, "vision_model.onnx");
string vocabPath = Path.Combine(modelDir, "vocab.json");
string mergesPath = Path.Combine(modelDir, "merges.txt");

var requiredFiles = new[]
{
    (textModelPath, "text_model.onnx"),
    (visionModelPath, "vision_model.onnx"),
    (vocabPath, "vocab.json"),
    (mergesPath, "merges.txt")
};

foreach (var (path, name) in requiredFiles)
{
    if (!File.Exists(path))
    {
        ImageRagChatConsoleRenderer.PrintError($"Required file not found: {name} (expected at: {path})");
        return;
    }
}

try
{
    var startTime = DateTime.Now;
    using var textEncoder = new ClipTextEncoder(textModelPath, vocabPath, mergesPath);
    using var imageEncoder = new ClipImageEncoder(visionModelPath);
    var loadTime = DateTime.Now - startTime;

    ImageRagChatConsoleRenderer.PrintSuccess($"Models loaded ({loadTime.TotalSeconds:F2}s)");
    ImageRagChatConsoleRenderer.PrintInfo("• Text encoder: CLIP ViT-B/32");
    ImageRagChatConsoleRenderer.PrintInfo("• Vision encoder: CLIP ViT-B/32");
    ImageRagChatConsoleRenderer.PrintInfo("• Embedding dimensions: 512");
    AnsiConsole.WriteLine();

    // =============================================================================
    // Step 3: Index Images
    // =============================================================================
    ImageRagChatConsoleRenderer.PrintStepHeader("Step 2: Indexing images");

    var searchEngine = new ImageSearchEngine(imageEncoder, textEncoder);

    startTime = DateTime.Now;

    string[] extensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
    var imageFiles = Directory.GetFiles(imageDir)
        .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .ToList();

    searchEngine.IndexImages(imageDir, (current, total) =>
    {
        if (current <= imageFiles.Count)
        {
            ImageRagChatConsoleRenderer.PrintIndexingProgress(current, total, Path.GetFileName(imageFiles[current - 1]));
        }
    });
    var indexTime = DateTime.Now - startTime;

    if (searchEngine.ImageCount == 0)
    {
        ImageRagChatConsoleRenderer.PrintError("No images found. Add images to the directory and try again.");
        return;
    }

    ImageRagChatConsoleRenderer.PrintSuccess($"Indexed {searchEngine.ImageCount} images in {indexTime.TotalSeconds:F2}s");
    AnsiConsole.WriteLine();

    // =============================================================================
    // Step 4: Interactive Chat Loop
    // =============================================================================
    ImageRagChatConsoleRenderer.PrintStepHeader("Step 3: Interactive Image Search");
    ImageRagChatConsoleRenderer.PrintInstructions();

    while (true)
    {
        var input = ImageRagChatConsoleRenderer.ReadUserInput();

        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            ImageRagChatConsoleRenderer.PrintInstructions();
            continue;
        }

        startTime = DateTime.Now;
        List<(string ImagePath, float Score)> results;

        if (input.StartsWith("image:", StringComparison.OrdinalIgnoreCase))
        {
            // Image-to-image search
            var imagePath = input["image:".Length..].Trim();
            if (!File.Exists(imagePath))
            {
                ImageRagChatConsoleRenderer.PrintError($"Image file not found: {imagePath}");
                continue;
            }

            results = searchEngine.SearchByImage(imagePath, topK: 5);
        }
        else
        {
            // Text-to-image search
            results = searchEngine.SearchByText(input, topK: 5);
        }

        var searchTime = DateTime.Now - startTime;

        if (results.Count == 0)
        {
            ImageRagChatConsoleRenderer.PrintNoResults();
        }
        else
        {
            ImageRagChatConsoleRenderer.PrintResults(results, input, searchTime);
        }
    }

    AnsiConsole.WriteLine();
    ImageRagChatConsoleRenderer.PrintGoodbye();
}
catch (Exception ex)
{
    ImageRagChatConsoleRenderer.PrintError($"Error: {ex.Message}");
    AnsiConsole.WriteException(ex);
}
