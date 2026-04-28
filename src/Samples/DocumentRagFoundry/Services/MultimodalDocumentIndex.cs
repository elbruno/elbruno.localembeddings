using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.ImageEmbeddings;
using Microsoft.Extensions.AI;

namespace DocumentRagFoundry.Services;

/// <summary>
/// Indexes and retrieves documents using both text and image embeddings
/// </summary>
public class MultimodalDocumentIndex : IDisposable
{
    private readonly LocalEmbeddingGenerator _textEmbeddings;
    private readonly ClipTextEncoder _clipTextEncoder;
    private readonly ClipImageEncoder _clipImageEncoder;

    private List<IndexedTextSegment> _textIndex = new();
    private List<IndexedImage> _imageIndex = new();

    public record IndexedTextSegment(
        string Text,
        string SourceFile,
        int PageNumber,
        Embedding<float> Embedding
    );

    public record IndexedImage(
        string ImagePath,
        string SourceFile,
        int PageNumber,
        Embedding<float> Embedding
    );

    public record SearchResult(
        string Content,
        string SourceFile,
        int PageNumber,
        string Type, // "text" or "image"
        float Similarity
    );

    public MultimodalDocumentIndex(
        string textModelPath,
        string visionModelPath,
        string vocabPath,
        string mergesPath)
    {
        _textEmbeddings = new LocalEmbeddingGenerator();
        _clipTextEncoder = new ClipTextEncoder(textModelPath, vocabPath, mergesPath);
        _clipImageEncoder = new ClipImageEncoder(visionModelPath);
    }

    /// <summary>
    /// Index text segments from PDFs
    /// </summary>
    public async Task IndexTextSegmentsAsync(IEnumerable<PdfProcessor.TextSegment> segments)
    {
        var texts = segments.Select(s => s.Text).ToArray();
        var embeddings = await _textEmbeddings.GenerateAsync(texts);

        var indexed = segments
            .Zip(embeddings, (segment, embedding) => new IndexedTextSegment(
                Text: segment.Text,
                SourceFile: segment.SourceFile,
                PageNumber: segment.PageNumber,
                Embedding: embedding
            ))
            .ToList();

        _textIndex.AddRange(indexed);
    }

    /// <summary>
    /// Index images (from PDFs or standalone)
    /// </summary>
    public async Task IndexImagesAsync(IEnumerable<PdfProcessor.PageImage> images)
    {
        var indexed = new List<IndexedImage>();

        await Task.Run(() =>
        {
            foreach (var image in images)
            {
                var embeddingArray = _clipImageEncoder.Encode(image.ImagePath);
                var embedding = new Embedding<float>(embeddingArray);

                indexed.Add(new IndexedImage(
                    ImagePath: image.ImagePath,
                    SourceFile: image.SourceFile,
                    PageNumber: image.PageNumber,
                    Embedding: embedding
                ));
            }
        });

        _imageIndex.AddRange(indexed);
    }

    /// <summary>
    /// Index standalone images (not from PDFs)
    /// </summary>
    public async Task IndexStandaloneImagesAsync(IEnumerable<string> imagePaths)
    {
        var indexed = new List<IndexedImage>();

        await Task.Run(() =>
        {
            foreach (var imagePath in imagePaths)
            {
                var embeddingArray = _clipImageEncoder.Encode(imagePath);
                var embedding = new Embedding<float>(embeddingArray);
                var fileName = Path.GetFileName(imagePath);

                indexed.Add(new IndexedImage(
                    ImagePath: imagePath,
                    SourceFile: fileName,
                    PageNumber: 0, // Standalone images don't have page numbers
                    Embedding: embedding
                ));
            }
        });

        _imageIndex.AddRange(indexed);
    }

    /// <summary>
    /// Search for relevant content using a text query
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, int topK = 5)
    {
        // Generate query embedding for text search
        var queryEmbedding = await _textEmbeddings.GenerateEmbeddingAsync(query);

        // Search text segments - convert to tuples for FindClosest
        var textTuples = _textIndex.Select(x => (Item: x, Embedding: x.Embedding));
        var textResults = textTuples
            .FindClosest(queryEmbedding, topK: topK)
            .Select(match => new SearchResult(
                Content: match.Item.Text,
                SourceFile: match.Item.SourceFile,
                PageNumber: match.Item.PageNumber,
                Type: "text",
                Similarity: match.Score
            ));

        // Search images using text query (CLIP's multimodal capability)
        var imageQueryEmbeddingArray = _clipTextEncoder.Encode(query);
        var imageQueryEmbedding = new Embedding<float>(imageQueryEmbeddingArray);

        var imageTuples = _imageIndex.Select(x => (Item: x, Embedding: x.Embedding));
        var imageResults = imageTuples
            .FindClosest(imageQueryEmbedding, topK: topK / 2) // Fewer images
            .Select(match => new SearchResult(
                Content: match.Item.ImagePath,
                SourceFile: match.Item.SourceFile,
                PageNumber: match.Item.PageNumber,
                Type: "image",
                Similarity: match.Score
            ));

        // Combine and sort by similarity
        var allResults = textResults.Concat(imageResults)
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();

        return allResults;
    }

    /// <summary>
    /// Get statistics about indexed content
    /// </summary>
    public (int TextSegments, int Images) GetIndexStats()
    {
        return (_textIndex.Count, _imageIndex.Count);
    }

    /// <summary>
    /// Clear all indexed content
    /// </summary>
    public void Clear()
    {
        _textIndex.Clear();
        _imageIndex.Clear();
    }

    public void Dispose()
    {
        _textEmbeddings?.Dispose();
        _clipTextEncoder?.Dispose();
        _clipImageEncoder?.Dispose();
    }
}
