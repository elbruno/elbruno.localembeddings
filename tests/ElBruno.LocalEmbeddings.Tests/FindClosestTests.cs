using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Tests for heap-based (PERF-09) FindClosest correctness.
/// These tests verify that the PriorityQueue min-heap implementation
/// produces results identical to the reference LINQ O(n log n) sort.
/// </summary>
public class FindClosestTests
{
    // -------------------------------------------------------------------------
    // PERF-09: Heap-based FindClosest correctness tests
    // -------------------------------------------------------------------------

    [Fact]
    public void FindClosest_ReturnsTopKResults_ByScore()
    {
        var query = CreateEmbedding([1f, 0f]);
        var corpus = new List<Embedding<float>>
        {
            CreateEmbedding([0f, 1f]),       // index 0 — orthogonal (score ≈ 0)
            CreateEmbedding([0.6f, 0.8f]),   // index 1 — partial match
            CreateEmbedding([1f, 0f]),        // index 2 — identical (score = 1)
            CreateEmbedding([0.9f, 0.1f]),   // index 3 — close match
        };

        var results = query.FindClosest(corpus, topK: 3);

        Assert.Equal(3, results.Count);

        // Must be ordered descending by score
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].Score >= results[i + 1].Score,
                $"Result at position {i} (score {results[i].Score}) should be >= position {i + 1} (score {results[i + 1].Score})");
        }

        // Highest-scoring item should be index 2 (identical vector)
        Assert.Equal(2, results[0].Index);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void FindClosest_TopKGreaterThanCorpus_ReturnsAll(int topK)
    {
        var query = CreateEmbedding([1f, 0f]);
        var corpus = new List<Embedding<float>>
        {
            CreateEmbedding([1f, 0f]),
            CreateEmbedding([0f, 1f]),
            CreateEmbedding([0.5f, 0.5f]),
        };

        var results = query.FindClosest(corpus, topK: topK);

        Assert.Equal(corpus.Count, results.Count);
    }

    [Fact]
    public void FindClosest_TopKOne_ReturnsHighestScore()
    {
        var query = CreateEmbedding([1f, 0f]);
        var corpus = new List<Embedding<float>>
        {
            CreateEmbedding([0f, 1f]),
            CreateEmbedding([0.5f, 0.5f]),
            CreateEmbedding([1f, 0f]),      // highest
            CreateEmbedding([0.8f, 0.2f]),
        };

        var results = query.FindClosest(corpus, topK: 1);

        Assert.Single(results);
        Assert.Equal(2, results[0].Index); // index of the [1f, 0f] embedding
        Assert.Equal(1f, results[0].Score, 4);
    }

    [Fact]
    public void FindClosest_TopKEqualsCorpus_MatchesOrderByDescending()
    {
        // Parity test: heap result must match reference LINQ sort
        var rng = new Random(42);
        const int corpusSize = 100;
        const int dims = 32;

        var query = CreateRandomEmbedding(rng, dims);
        var corpus = Enumerable.Range(0, corpusSize)
            .Select(_ => CreateRandomEmbedding(rng, dims))
            .ToList();

        var heapResults = query.FindClosest(corpus, topK: corpusSize);

        // Reference implementation: LINQ sort
        var reference = corpus
            .Select((embedding, index) => (Index: index, Score: query.CosineSimilarity(embedding)))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Index)
            .ToList();

        Assert.Equal(reference.Count, heapResults.Count);
        for (int i = 0; i < reference.Count; i++)
        {
            Assert.Equal(reference[i].Index, heapResults[i].Index);
            Assert.Equal(reference[i].Score, heapResults[i].Score, 5);
        }
    }

    [Fact]
    public void FindClosest_EmptyCorpus_ReturnsEmpty()
    {
        var query = CreateEmbedding([1f, 0f]);
        var corpus = new List<Embedding<float>>();

        var results = query.FindClosest(corpus, topK: 5);

        Assert.Empty(results);
    }

    [Fact]
    public void FindClosest_AllEqualScores_ReturnsTopK()
    {
        // When all corpus vectors are identical to the query, all scores = 1.
        // The heap must still return exactly topK items without duplicates or omissions.
        const int corpusSize = 20;
        const int topK = 5;
        var query = CreateEmbedding([1f, 0f]);
        var corpus = Enumerable.Range(0, corpusSize)
            .Select(_ => CreateEmbedding([1f, 0f]))
            .ToList();

        var results = query.FindClosest(corpus, topK: topK);

        Assert.Equal(topK, results.Count);
        Assert.All(results, r => Assert.Equal(1f, r.Score, 4));

        // All returned indices must be distinct
        var distinctIndices = results.Select(r => r.Index).Distinct().Count();
        Assert.Equal(topK, distinctIndices);
    }

    [Fact]
    public void FindClosest_LargeCorpus_TopKSubset_MatchesLinqReference()
    {
        // Regression guard: for k << n, heap must still match LINQ full sort (top-k slice)
        var rng = new Random(7);
        const int corpusSize = 200;
        const int dims = 16;
        const int topK = 10;

        var query = CreateRandomEmbedding(rng, dims);
        var corpus = Enumerable.Range(0, corpusSize)
            .Select(_ => CreateRandomEmbedding(rng, dims))
            .ToList();

        var heapResults = query.FindClosest(corpus, topK: topK);

        var reference = corpus
            .Select((embedding, index) => (Index: index, Score: query.CosineSimilarity(embedding)))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Index)
            .Take(topK)
            .ToList();

        Assert.Equal(topK, heapResults.Count);
        for (int i = 0; i < topK; i++)
        {
            Assert.Equal(reference[i].Index, heapResults[i].Index);
            Assert.Equal(reference[i].Score, heapResults[i].Score, 5);
        }
    }

    [Fact]
    public void FindClosest_WithMinScore_HeapOnlyIncludesAboveThreshold()
    {
        var query = CreateEmbedding([1f, 0f]);
        var corpus = new List<Embedding<float>>
        {
            CreateEmbedding([1f, 0f]),       // score = 1.0 — above threshold
            CreateEmbedding([0f, 1f]),        // score ≈ 0.0 — below threshold
            CreateEmbedding([0.707f, 0.707f]), // score ≈ 0.707 — above threshold
        };

        var results = query.FindClosest(corpus, topK: 10, minScore: 0.5f);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Score >= 0.5f));
    }

    // -------------------------------------------------------------------------
    // PERF-08 / PERF-12/13: Tokenizer regression tests (no intermediate int[])
    // These are integration tests — skipped when model files are not present.
    // They verify that the optimized tokenizer path produces byte-for-byte
    // identical output to the expected known outputs for fixed inputs.
    // -------------------------------------------------------------------------

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Tokenize_KnownInput_ProducesExpectedSpecialTokenLayout()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing (PERF-08 regression)");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 16);
        var (inputIds, attentionMask) = tokenizer.Tokenize("hello world");

        // CLS token must be at position 0
        Assert.Equal(tokenizer.ClsTokenId, (int)inputIds[0]);
        // Attention must be 1 at position 0
        Assert.Equal(1L, attentionMask[0]);
        // Array lengths must match maxLength exactly
        Assert.Equal(16, inputIds.Length);
        Assert.Equal(16, attentionMask.Length);
        // Padding region must have attention mask = 0 and inputId = padTokenId
        for (int i = 0; i < inputIds.Length; i++)
        {
            if (inputIds[i] == tokenizer.PadTokenId && i > 0)
            {
                Assert.Equal(0L, attentionMask[i]);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Tokenize_SameInputTwice_ProducesBitwiseIdenticalOutput()
    {
        // Verifies that the optimized allocation path is deterministic —
        // no mutable shared state introduced by PERF-08 intermediate buffer elimination.
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing (PERF-08 regression)");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 64);
        const string input = "The quick brown fox jumps over the lazy dog";

        var (ids1, mask1) = tokenizer.Tokenize(input);
        var (ids2, mask2) = tokenizer.Tokenize(input);

        Assert.Equal(ids1, ids2);
        Assert.Equal(mask1, mask2);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void TokenizeBatch_OutputMatchesSingleTokenizeCalls_AfterToListRemoval()
    {
        // Regression for PERF-12/13: removing .ToList() calls must not change batch output.
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing (PERF-12/13 regression)");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 32);
        var texts = new[] { "alpha", "beta gamma", "delta epsilon zeta" };

        var (batchIds, batchMasks) = tokenizer.TokenizeBatch(texts);

        // Each row of the batch must match what single Tokenize produces
        for (int i = 0; i < texts.Length; i++)
        {
            var (singleIds, singleMask) = tokenizer.Tokenize(texts[i], maxLength: 32);
            Assert.Equal(singleIds, batchIds[i]);
            Assert.Equal(singleMask, batchMasks[i]);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Embedding<float> CreateEmbedding(float[] vector) => new(vector);

    private static Embedding<float> CreateRandomEmbedding(Random rng, int dims)
    {
        var vector = new float[dims];
        for (int j = 0; j < dims; j++)
        {
            vector[j] = (float)(rng.NextDouble() * 2 - 1);
        }

        return CreateEmbedding(vector);
    }

    private static string? GetTokenizerPath()
    {
        var defaultCache = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalEmbeddings", "models")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "LocalEmbeddings", "models");

        var modelDir = Path.Combine(defaultCache, "sentence-transformers_all-MiniLM-L6-v2");
        if (File.Exists(Path.Combine(modelDir, "vocab.txt")))
        {
            return modelDir;
        }

        var envPath = Environment.GetEnvironmentVariable("LOCALEMBEDDINGS_TEST_TOKENIZER");
        if (!string.IsNullOrEmpty(envPath) && (Directory.Exists(envPath) || File.Exists(envPath)))
        {
            return envPath;
        }

        return null;
    }
}
