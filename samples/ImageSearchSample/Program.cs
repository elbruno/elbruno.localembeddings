namespace ImageSearchSample;

/// <summary>
/// CLIP-based text-to-image semantic search demonstration.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== CLIP Image Search Sample ===\n");

        // Parse arguments
        if (!TryParseArguments(args, out string modelDir, out string imageDir))
        {
            Console.WriteLine("Usage: ImageSearchSample --model-dir <model-directory> --image-dir <image-directory>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  --model-dir, -m   - Directory containing CLIP ONNX models:");
            Console.WriteLine("                      - text_model.onnx (text encoder)");
            Console.WriteLine("                      - vision_model.onnx (image encoder)");
            Console.WriteLine("                      - vocab.json (vocabulary)");
            Console.WriteLine("                      - merges.txt (BPE merges)");
            Console.WriteLine("  --image-dir, -i   - Directory containing images to search");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run --project samples/ImageSearchSample -- --model-dir ./clip-models --image-dir ./my-images");
            Console.WriteLine();
            Console.WriteLine("See README.md for model download instructions.");
            return;
        }

        // Validate paths
        if (!Directory.Exists(modelDir))
        {
            Console.WriteLine($"Error: Model directory not found: {modelDir}");
            return;
        }

        if (!Directory.Exists(imageDir))
        {
            Console.WriteLine($"Error: Image directory not found: {imageDir}");
            return;
        }

        // Model paths
        string textModelPath = Path.Combine(modelDir, "text_model.onnx");
        string visionModelPath = Path.Combine(modelDir, "vision_model.onnx");
        string vocabPath = Path.Combine(modelDir, "vocab.json");
        string mergesPath = Path.Combine(modelDir, "merges.txt");

        // Validate model files
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
                Console.WriteLine($"Error: Required file not found: {name}");
                Console.WriteLine($"Expected at: {path}");
                return;
            }
        }

        try
        {
            // Initialize encoders
            Console.WriteLine("Loading CLIP models...");
            using var textEncoder = new ClipTextEncoder(textModelPath, vocabPath, mergesPath);
            using var imageEncoder = new ClipImageEncoder(visionModelPath);
            Console.WriteLine("Models loaded successfully.\n");

            // Initialize search engine
            var searchEngine = new ImageSearchEngine(imageEncoder, textEncoder);

            // Index images
            searchEngine.IndexImages(imageDir);

            if (searchEngine.ImageCount == 0)
            {
                Console.WriteLine("No images found to index. Please add images to the directory.");
                return;
            }

            // Interactive search loop
            Console.WriteLine("Enter a search query (or 'exit' to quit):");
            while (true)
            {
                Console.Write("\n> ");
                string? query = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(query))
                    continue;

                if (query.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    query.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // Search
                var results = searchEngine.Search(query, topK: 5);

                // Display results
                Console.WriteLine($"\nTop {results.Count} results for \"{query}\":");
                Console.WriteLine(new string('-', 60));

                for (int i = 0; i < results.Count; i++)
                {
                    var (imagePath, score) = results[i];
                    Console.WriteLine($"{i + 1}. {Path.GetFileName(imagePath)} (score: {score:F4})");
                }

                Console.WriteLine(new string('-', 60));
            }

            Console.WriteLine("\nThank you for using CLIP Image Search!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static bool TryParseArguments(string[] args, out string modelDir, out string imageDir)
    {
        modelDir = string.Empty;
        imageDir = string.Empty;

        if (TryGetOptionValue(args, "--model-dir", "-m", out string? modelValue) &&
            TryGetOptionValue(args, "--image-dir", "-i", out string? imageValue))
        {
            modelDir = modelValue;
            imageDir = imageValue;
            return true;
        }

        if (args.Length >= 2)
        {
            modelDir = args[0];
            imageDir = args[1];
            return true;
        }

        return false;
    }

    private static bool TryGetOptionValue(string[] args, string longName, string shortName, out string? value)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.Equals(longName, StringComparison.OrdinalIgnoreCase) ||
                arg.Equals(shortName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    value = args[i + 1];
                    return true;
                }

                value = null;
                return false;
            }

            if (arg.StartsWith(longName + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg[(longName.Length + 1)..];
                return true;
            }
        }

        value = null;
        return false;
    }
}
