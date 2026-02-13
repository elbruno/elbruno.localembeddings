#pragma warning disable CS8602

using ElBruno.LocalEmbeddings.KernelMemory.Extensions;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using OllamaSharp;

var ollamaEndpoint = "http://localhost:11434";
var modelIdChat = "phi4-mini";
var question = "What is Bruno's favourite super hero?";

// --- Ask without memory ---
Console.WriteLine($"Question: {question}");
Console.WriteLine($"\n--- {modelIdChat} response (no memory) ---");

var ollama = new OllamaApiClient(ollamaEndpoint) { SelectedModel = modelIdChat };
await foreach (var token in ollama.GenerateAsync(question))
    Console.Write(token.Response);

// --- Build Kernel Memory with local embeddings ---
Console.WriteLine("\n\n--- Importing facts into Kernel Memory ---");

var config = new OllamaConfig
{
    Endpoint = ollamaEndpoint,
    TextModel = new OllamaModelConfig(modelIdChat)
};

var memory = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(config)
    .WithLocalEmbeddings()
    .WithCustomTextPartitioningOptions(new TextPartitioningOptions
    {
        MaxTokensPerParagraph = 256,
        OverlappingTokens = 50
    })
    .Build();

var facts = new[]
{
    "Gisela's favourite super hero is Batman",
    "Gisela watched Venom 3 2 weeks ago",
    "Bruno's favourite super hero is Invincible",
    "Bruno went to the cinema to watch Venom 3",
    "Bruno doesn't like the super hero movie: Eternals",
    "ACE and Goku watched the movies Venom 3 and Eternals",
};

for (var i = 0; i < facts.Length; i++)
{
    Console.WriteLine($"  [{i + 1}] {facts[i]}");
    await memory.ImportTextAsync(facts[i], (i + 1).ToString());
}

// --- Ask with memory ---
Console.WriteLine($"\n--- Asking with memory: {question} ---");
await foreach (var result in memory.AskStreamingAsync(question))
{
    Console.Write(result.Result);

    if (result.RelevantSources.Count > 0)
    {
        Console.WriteLine("\n\n--- Relevant Sources ---");
        foreach (var source in result.RelevantSources)
        {
            Console.WriteLine($"  [source Url: #{source.Index}] Relevance: {source.SourceUrl}");
        }
    }

}

Console.WriteLine();
