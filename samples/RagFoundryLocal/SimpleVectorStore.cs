using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;

namespace RagFoundryLocal;

internal sealed class SimpleVectorStore(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    private readonly List<(string Content, Embedding<float> Embedding)> _entries = [];

    public async Task AddAsync(IEnumerable<string> documents, CancellationToken cancellationToken = default)
    {
        var docs = documents.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (docs.Count == 0)
        {
            return;
        }

        var embeddings = await embeddingGenerator.GenerateAsync(docs, cancellationToken: cancellationToken);
        for (var i = 0; i < docs.Count; i++)
        {
            _entries.Add((docs[i], embeddings[i]));
        }
    }

    public async Task<List<(string Content, float Score)>> SearchAsync(string query, int topK = 3, CancellationToken cancellationToken = default)
    {
        if (_entries.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var queryEmbedding = (await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken))[0];
        return _entries
            .Select(e => (e.Content, Score: queryEmbedding.CosineSimilarity(e.Embedding)))
            .OrderByDescending(e => e.Score)
            .Take(topK)
            .ToList();
    }
}
