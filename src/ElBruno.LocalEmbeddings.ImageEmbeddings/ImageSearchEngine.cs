using System.Numerics.Tensors;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings;

/// <summary>
/// Image search engine using CLIP embeddings and cosine similarity.
/// </summary>
/// <remarks>
/// Provides functionality to index images by computing their CLIP embeddings
/// and search the index using natural language text queries or image queries.
/// Both text and image embeddings are L2-normalized, so cosine similarity
/// reduces to the dot product.
/// </remarks>
public sealed class ImageSearchEngine
{
    private readonly ClipImageEncoder _imageEncoder;
    private readonly ClipTextEncoder _textEncoder;
    private readonly List<(string Path, float[] Embedding)> _imageIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageSearchEngine"/> class.
    /// </summary>
    /// <param name="imageEncoder">The CLIP image encoder for computing image embeddings.</param>
    /// <param name="textEncoder">The CLIP text encoder for computing text query embeddings.</param>
    public ImageSearchEngine(ClipImageEncoder imageEncoder, ClipTextEncoder textEncoder)
    {
        _imageEncoder = imageEncoder;
        _textEncoder = textEncoder;
        _imageIndex = [];
    }

    /// <summary>
    /// Gets the number of indexed images.
    /// </summary>
    public int ImageCount => _imageIndex.Count;

    /// <summary>
    /// Indexes all images in the specified directory.
    /// </summary>
    /// <param name="imageDirectory">Path to the directory containing images to index.</param>
    /// <param name="progress">Optional progress callback receiving (current, total, fileName).</param>
    /// <exception cref="DirectoryNotFoundException">Thrown when the image directory does not exist.</exception>
    public void IndexImages(string imageDirectory, Action<int, int, string>? progress = null)
    {
        if (!Directory.Exists(imageDirectory))
        {
            throw new DirectoryNotFoundException($"Image directory not found: {imageDirectory}");
        }

        string[] extensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
        var imageFiles = Directory.GetFiles(imageDirectory)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        for (int i = 0; i < imageFiles.Count; i++)
        {
            var imagePath = imageFiles[i];
            var embedding = _imageEncoder.Encode(imagePath);
            _imageIndex.Add((imagePath, embedding));
            var fileName = GetProgressFileName(imagePath);
            progress?.Invoke(i + 1, imageFiles.Count, fileName);
        }
    }

    /// <summary>
    /// Adds a single image to the index.
    /// </summary>
    /// <param name="imagePath">Path to the image file to index.</param>
    /// <exception cref="FileNotFoundException">Thrown when the image file does not exist.</exception>
    public void AddImage(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Image file not found: {imagePath}");
        }

        var embedding = _imageEncoder.Encode(imagePath);
        _imageIndex.Add((imagePath, embedding));
    }

    /// <summary>
    /// Searches for images matching the text query using cosine similarity.
    /// </summary>
    /// <param name="query">The natural language text query.</param>
    /// <param name="topK">The maximum number of results to return. Default is 5.</param>
    /// <returns>A list of image paths with their similarity scores, ordered by descending score.</returns>
    public List<(string ImagePath, float Score)> SearchByText(string query, int topK = 5)
    {
        if (_imageIndex.Count == 0)
        {
            return [];
        }

        var queryEmbedding = _textEncoder.Encode(query);
        return RankResults(queryEmbedding, topK);
    }

    /// <summary>
    /// Searches for images similar to the specified image using cosine similarity.
    /// </summary>
    /// <param name="imagePath">Path to the query image file.</param>
    /// <param name="topK">The maximum number of results to return. Default is 5.</param>
    /// <returns>A list of image paths with their similarity scores, ordered by descending score.</returns>
    public List<(string ImagePath, float Score)> SearchByImage(string imagePath, int topK = 5)
    {
        if (_imageIndex.Count == 0)
        {
            return [];
        }

        var queryEmbedding = _imageEncoder.Encode(imagePath);
        return RankResults(queryEmbedding, topK);
    }

    private List<(string ImagePath, float Score)> RankResults(float[] queryEmbedding, int topK)
    {
        var results = new List<(string Path, float Score)>();
        foreach (var (path, imageEmbedding) in _imageIndex)
        {
            float similarity = TensorPrimitives.CosineSimilarity(
                queryEmbedding.AsSpan(),
                imageEmbedding.AsSpan());
            results.Add((path, similarity));
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    internal static string GetProgressFileName(string imagePath) => Path.GetFileName(imagePath);
}
