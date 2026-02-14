using Microsoft.Extensions.AI;

namespace RagChat.VectorStore;

/// <summary>
/// A simple in-memory vector store for RAG-style applications.
/// </summary>
/// <remarks>
/// This implementation stores documents with their embeddings and supports
/// similarity search using cosine similarity. Suitable for demos and small datasets.
/// </remarks>
public sealed class InMemoryVectorStore
{
    private readonly List<Document> _documents = [];
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryVectorStore"/> class.
    /// </summary>
    /// <param name="embeddingGenerator">The embedding generator to use for vectorizing documents.</param>
    public InMemoryVectorStore(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
    }

    /// <summary>
    /// Gets the number of documents currently stored.
    /// </summary>
    public int Count => _documents.Count;

    /// <summary>
    /// Adds a single document and generates its embedding.
    /// </summary>
    /// <param name="document">The document to add.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task AddDocumentAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var embeddings = await _embeddingGenerator.GenerateAsync([document.Content], cancellationToken: cancellationToken);
        document.Embedding = embeddings[0].Vector.ToArray();
        _documents.Add(document);
    }

    /// <summary>
    /// Adds multiple documents and generates their embeddings in batch.
    /// </summary>
    /// <param name="documents">The documents to add.</param>
    /// <param name="progressCallback">Optional callback to report progress.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task AddDocumentsAsync(
        IEnumerable<Document> documents, 
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var docList = documents.ToList();
        if (docList.Count == 0) return;

        // Generate embeddings in batch for efficiency
        var contents = docList.Select(d => d.Content).ToList();
        var embeddings = await _embeddingGenerator.GenerateAsync(contents, cancellationToken: cancellationToken);

        for (int i = 0; i < docList.Count; i++)
        {
            docList[i].Embedding = embeddings[i].Vector.ToArray();
            _documents.Add(docList[i]);
            progressCallback?.Invoke(i + 1, docList.Count);
        }
    }

    /// <summary>
    /// Searches for documents similar to the query using cosine similarity.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="minScore">The minimum similarity score threshold (0-1).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of documents with their similarity scores, ordered by relevance.</returns>
    public async Task<List<SearchResult>> SearchAsync(
        string query, 
        int topK = 5, 
        float minScore = 0.0f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));

        if (_documents.Count == 0)
            return [];

        // Generate embedding for the query
        var queryEmbeddings = await _embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
        var queryVector = queryEmbeddings[0].Vector.ToArray();

        // Calculate similarity with all documents
        var results = _documents
            .Where(d => d.Embedding is not null)
            .Select(doc => new SearchResult
            {
                Document = doc,
                Score = CosineSimilarity(queryVector, doc.Embedding!)
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return results;
    }

    /// <summary>
    /// Gets all documents in the store.
    /// </summary>
    public IReadOnlyList<Document> GetAllDocuments() => _documents.AsReadOnly();

    /// <summary>
    /// Clears all documents from the store.
    /// </summary>
    public void Clear() => _documents.Clear();

    /// <summary>
    /// Calculates cosine similarity between two vectors.
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length.");

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}

/// <summary>
/// Represents a search result with document and similarity score.
/// </summary>
public sealed class SearchResult
{
    /// <summary>
    /// Gets the matched document.
    /// </summary>
    public required Document Document { get; init; }

    /// <summary>
    /// Gets the similarity score (0-1, where 1 is most similar).
    /// </summary>
    public required float Score { get; init; }
}
