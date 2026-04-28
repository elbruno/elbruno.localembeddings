using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text;
using System.Text.Json;

namespace DocumentRagFoundry.Services;

/// <summary>
/// Generates answers using Foundry Local LLM with retrieved context
/// </summary>
public class DocumentQaGenerator : IAsyncDisposable
{
    private readonly FoundryLocalManager _manager;
    private readonly IChatClient _chatClient;
    private readonly string _modelId;

    private DocumentQaGenerator(FoundryLocalManager manager, IChatClient chatClient, string modelId)
    {
        _manager = manager;
        _chatClient = chatClient;
        _modelId = modelId;
    }

    /// <summary>
    /// Creates a new DocumentQaGenerator with Foundry Local phi-4-mini
    /// </summary>
    public static async Task<DocumentQaGenerator> CreateAsync(string modelAlias = "phi-4-mini")
    {
        var manager = await FoundryLocalManager.StartModelAsync(modelAlias);
        var modelId = await ResolveModelIdAsync(manager.Endpoint, modelAlias);

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(manager.ApiKey),
            new OpenAIClientOptions { Endpoint = manager.Endpoint }
        );

        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();

        return new DocumentQaGenerator(manager, chatClient, modelId);
    }

    /// <summary>
    /// Generates an answer to a question using retrieved context (streaming)
    /// </summary>
    public async Task<string> GenerateAnswerAsync(
        string question,
        IEnumerable<MultimodalDocumentIndex.SearchResult> context,
        Action<string>? onStreamUpdate = null)
    {
        var prompt = BuildPrompt(question, context);
        var answer = new StringBuilder();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)]))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                answer.Append(update.Text);
                onStreamUpdate?.Invoke(update.Text);
            }
        }

        return answer.ToString();
    }

    private static string BuildPrompt(
        string question,
        IEnumerable<MultimodalDocumentIndex.SearchResult> context)
    {
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Retrieved Context:");
        contextBuilder.AppendLine();

        foreach (var result in context)
        {
            if (result.Type == "text")
            {
                contextBuilder.AppendLine($"[{result.SourceFile} - Page {result.PageNumber}]");
                contextBuilder.AppendLine(result.Content);
                contextBuilder.AppendLine();
            }
            else if (result.Type == "image")
            {
                contextBuilder.AppendLine($"[Image: {result.SourceFile} - Page {result.PageNumber}]");
                contextBuilder.AppendLine($"Path: {result.Content}");
                contextBuilder.AppendLine();
            }
        }

        return $"""
You are a helpful assistant analyzing documents. Use the provided context to answer questions accurately and concisely.

{contextBuilder}

Question: {question}

Instructions:
- Answer based on the retrieved context above
- Cite sources when referring to specific information (e.g., "According to document.pdf, page 2...")
- If images are mentioned in the context, acknowledge them in your answer
- If the context doesn't contain enough information, say so
- Be concise but thorough

Answer:
""";
    }

    private static async Task<string> ResolveModelIdAsync(Uri endpoint, string alias)
    {
        using var http = new HttpClient { BaseAddress = endpoint };
        var json = await http.GetStringAsync("/v1/models");
        using var doc = JsonDocument.Parse(json);

        foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            string id = model.GetProperty("id").GetString()!;
            if (id.Contains(alias, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return alias;
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager != null)
        {
            await _manager.DisposeAsync();
        }
    }
}
