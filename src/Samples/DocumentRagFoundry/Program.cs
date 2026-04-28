using DocumentRagFoundry.Services;
using Spectre.Console;

const string docsPath = "../docs";
const string defaultModelDir = "./scripts/clip-models";
const string tempImagePath = "./temp_pdf_images";

// Parse arguments
string modelDir = args.Length > 0 ? args[0] : defaultModelDir;

if (!Directory.Exists(modelDir))
{
    AnsiConsole.MarkupLine("[bold red]Error:[/] Model directory not found");
    AnsiConsole.MarkupLine($"[dim]Expected: {modelDir}[/]");
    AnsiConsole.MarkupLine("\nRun the CLIP model download script:");
    AnsiConsole.MarkupLine("[yellow]./scripts/download_clip_models.ps1[/]");
    return;
}

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
        AnsiConsole.MarkupLine($"[bold red]Error:[/] Required file not found: {name}");
        AnsiConsole.MarkupLine($"[dim]Expected at: {path}[/]");
        AnsiConsole.MarkupLine("\nRun the CLIP model download script:");
        AnsiConsole.MarkupLine("[yellow]./scripts/download_clip_models.ps1[/]");
        return;
    }
}

AnsiConsole.Write(
    new FigletText("Document RAG")
        .LeftJustified()
        .Color(Color.Blue));

AnsiConsole.MarkupLine("[bold]Multimodal Document Analysis with Foundry Local[/]\n");

await using var qaGenerator = await AnsiConsole.Status()
    .StartAsync("Starting Foundry Local (phi-4-mini)...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Dots);
        return await DocumentQaGenerator.CreateAsync("phi-4-mini");
    });

AnsiConsole.MarkupLine("[green]✓[/] Foundry Local ready\n");

// Initialize document index
using var docIndex = new MultimodalDocumentIndex(textModelPath, visionModelPath, vocabPath, mergesPath);
var pdfProcessor = new PdfProcessor();

// Load and index documents
await AnsiConsole.Status()
    .StartAsync("Indexing documents...", async ctx =>
    {
        ctx.Status("Looking for PDFs...");
        var pdfFiles = Directory.Exists(docsPath)
            ? Directory.GetFiles(docsPath, "*.pdf")
            : Array.Empty<string>();

        if (pdfFiles.Length > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Found {pdfFiles.Length} PDF(s)[/]");

            foreach (var pdfFile in pdfFiles)
            {
                var fileName = Path.GetFileName(pdfFile);
                ctx.Status($"Processing {fileName}...");

                try
                {
                    // Extract text
                    var textSegments = await pdfProcessor.ExtractTextAsync(pdfFile);
                    if (textSegments.Count > 0)
                    {
                        await docIndex.IndexTextSegmentsAsync(textSegments);
                        AnsiConsole.MarkupLine($"[dim]  ├─ Indexed {textSegments.Count} text segment(s) from {fileName}[/]");
                    }

                    // Convert pages to images
                    var pageImages = await pdfProcessor.ConvertPagesToImagesAsync(pdfFile, tempImagePath);
                    if (pageImages.Count > 0)
                    {
                        await docIndex.IndexImagesAsync(pageImages);
                        AnsiConsole.MarkupLine($"[dim]  └─ Indexed {pageImages.Count} page image(s) from {fileName}[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]  └─ Warning: Could not process {fileName}: {ex.Message}[/]");
                }
            }
        }

        // Index standalone images
        ctx.Status("Looking for standalone images...");
        var imageExtensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" };
        var imageFiles = Directory.Exists(docsPath)
            ? imageExtensions.SelectMany(ext => Directory.GetFiles(docsPath, ext)).ToArray()
            : Array.Empty<string>();

        if (imageFiles.Length > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Found {imageFiles.Length} image(s)[/]");
            await docIndex.IndexStandaloneImagesAsync(imageFiles);

            foreach (var imageFile in imageFiles)
            {
                AnsiConsole.MarkupLine($"[dim]  ├─ Indexed {Path.GetFileName(imageFile)}[/]");
            }
        }
    });

var stats = docIndex.GetIndexStats();
AnsiConsole.MarkupLine($"\n[green]✓[/] Indexing complete: {stats.TextSegments} text segments, {stats.Images} images\n");

if (stats.TextSegments == 0 && stats.Images == 0)
{
    AnsiConsole.MarkupLine($"[yellow]Warning:[/] No documents found in '{docsPath}'");
    AnsiConsole.MarkupLine("[dim]Please add PDF files and/or images to the docs folder[/]\n");
}

// Interactive query loop
AnsiConsole.MarkupLine("[bold]Ask questions about your documents![/]");
AnsiConsole.MarkupLine("[dim]Type 'exit' to quit[/]\n");

while (true)
{
    var question = AnsiConsole.Prompt(
        new TextPrompt<string>("[blue]Question:[/]")
            .PromptStyle("white")
            .AllowEmpty());

    if (string.IsNullOrWhiteSpace(question))
    {
        continue;
    }

    if (question.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (stats.TextSegments == 0 && stats.Images == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No documents indexed. Please add files to the docs folder and restart.[/]\n");
        continue;
    }

    try
    {
        // Search for relevant context
        var results = await AnsiConsole.Status()
            .StartAsync("Searching...", async ctx =>
            {
                return await docIndex.SearchAsync(question, topK: 5);
            });

        // Display retrieved context
        if (results.Count > 0)
        {
            AnsiConsole.MarkupLine("\n[dim]Retrieved Context:[/]");
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn("Type")
                .AddColumn("Source")
                .AddColumn("Page")
                .AddColumn("Preview/Path")
                .AddColumn("Score");

            foreach (var result in results)
            {
                var preview = result.Type == "text"
                    ? (result.Content.Length > 60 ? result.Content[..60] + "..." : result.Content)
                    : Path.GetFileName(result.Content);

                var pageDisplay = result.PageNumber > 0 ? result.PageNumber.ToString() : "-";

                table.AddRow(
                    result.Type == "text" ? "[cyan]Text[/]" : "[yellow]Image[/]",
                    result.SourceFile,
                    pageDisplay,
                    preview.EscapeMarkup(),
                    $"{result.Similarity:F3}"
                );
            }

            AnsiConsole.Write(table);
        }

        // Generate answer
        AnsiConsole.MarkupLine("\n[bold green]Answer:[/]");
        AnsiConsole.Write(new Rule().RuleStyle("dim"));

        await qaGenerator.GenerateAnswerAsync(
            question,
            results,
            onStreamUpdate: text => AnsiConsole.Markup(text.EscapeMarkup())
        );

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle("dim"));
        AnsiConsole.WriteLine();
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}\n");
    }
}

AnsiConsole.MarkupLine("\n[dim]Goodbye![/]");

// Cleanup temp images
if (Directory.Exists(tempImagePath))
{
    try
    {
        Directory.Delete(tempImagePath, true);
    }
    catch
    {
        // Ignore cleanup errors
    }
}
