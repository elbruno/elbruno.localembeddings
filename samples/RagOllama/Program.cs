#pragma warning disable CS8602

using ElBruno.LocalEmbeddings.KernelMemory.Extensions;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using OllamaSharp;

var ollamaEndpoint = "http://localhost:11434";
var modelIdChat = "phi3.5";

// questions
var question = "What is Bruno's favourite super hero?";

// intro
SpectreConsoleOutput.DisplayTitle(modelIdChat);
SpectreConsoleOutput.DisplayTitleH2($"This program will answer the following question:");
SpectreConsoleOutput.DisplayTitleH3(question);
SpectreConsoleOutput.DisplayTitleH2($"Approach:");
SpectreConsoleOutput.DisplayTitleH3($"1st approach will be to ask the question directly to the {modelIdChat} model.");
SpectreConsoleOutput.DisplayTitleH3("2nd approach will be to add facts to a semantic memory and ask the question again");

SpectreConsoleOutput.DisplayTitleH2($"{modelIdChat} response (no memory).");

// set up the client
var ollama = new OllamaApiClient(ollamaEndpoint)
{
    SelectedModel = modelIdChat
};

await foreach (var answerToken in ollama.GenerateAsync(question))
{
    Console.Write(answerToken.Response.ToString());
}
;

// separator
Console.WriteLine("");
SpectreConsoleOutput.DisplaySeparator();

var configOllamaKernelMemory = new OllamaConfig
{
    Endpoint = ollamaEndpoint,
    TextModel = new OllamaModelConfig(modelIdChat)
};

var memory = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(configOllamaKernelMemory)
    .WithLocalEmbeddings()
    .Build();

var informationList = new List<string>
{
    "Gisela's favourite super hero is Batman",
    "Gisela watched Venom 3 2 weeks ago",
    "Bruno's favourite super hero is Invincible",
    "Bruno went to the cinema to watch Venom 3",
    "Bruno doesn't like the super hero movie: Eternals",
    "ACE and Goku watched the movies Venom 3 and Eternals",
};

SpectreConsoleOutput.DisplayTitleH2($"Information List");

int docId = 1;
foreach (var info in informationList)
{
    SpectreConsoleOutput.WriteYellow($"Adding docId: {docId} - information: {info}", true);
    await memory.ImportTextAsync(info, docId.ToString());
    docId++;
}

SpectreConsoleOutput.DisplayTitleH3($"Asking question with memory: {question}");
var answer = memory.AskStreamingAsync(question);
await foreach (var result in answer)
{
    SpectreConsoleOutput.WriteGreen($"{result.Result}");
    SpectreConsoleOutput.DisplayNewLine();

}

Console.WriteLine($"");
