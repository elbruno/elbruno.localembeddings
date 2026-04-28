using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          MCP Tool Router Sample Application                    ║");
Console.WriteLine("║    Semantic tool discovery with local embeddings              ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var tools = new List<(string Name, string Description)>
{
    ("search_web", "Search the internet for information"),
    ("send_email", "Send email messages"),
    ("create_file", "Create files on the filesystem"),
    ("analyze_data", "Perform statistical analysis")
};

Console.WriteLine($"Created {tools.Count} tool definitions");
Console.WriteLine();

var embedder = await LocalEmbeddingGenerator.CreateAsync(new LocalEmbeddingsOptions { EnsureModelDownloaded = true });
var toolDescriptions = tools.Select(t => t.Description).ToList();
var toolEmbeddings = await embedder.GenerateAsync(toolDescriptions);

Console.WriteLine($"✓ Indexed {tools.Count} tools");
Console.WriteLine();

var query = "I need to find information online";
var results = await embedder.FindClosestAsync(query, toolDescriptions, toolEmbeddings, topK: 3);

Console.WriteLine($"Query: \"{query}\"");
foreach (var result in results)
{
    var toolIndex = toolDescriptions.IndexOf(result.Text);
    Console.WriteLine($"  • {tools[toolIndex].Name} ({result.Score:F4})");
}