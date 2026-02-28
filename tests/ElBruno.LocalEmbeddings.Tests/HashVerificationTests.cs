using System.Security.Cryptography;
using System.Net;
using ElBruno.LocalEmbeddings.Options;
using Moq;
using Moq.Protected;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Tests for SEC-001: SHA-256 sidecar hash verification in ModelDownloader.
/// </summary>
public class HashVerificationTests
{
    // -------------------------------------------------------------------------
    // Pure SHA-256 algorithm correctness
    // -------------------------------------------------------------------------

    [Fact]
    public void HashVerification_Sha256_ProducesCorrectLength()
    {
        var content = "model file content"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        // SHA-256 = 32 bytes = 64 lower-case hex characters
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void HashVerification_SameContent_ProducesSameHash()
    {
        var content = "model file content"u8.ToArray();
        var hash1 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var hash2 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashVerification_DifferentContent_ProducesDifferentHash()
    {
        var hash1 = Convert.ToHexString(SHA256.HashData("original"u8.ToArray())).ToLowerInvariant();
        var hash2 = Convert.ToHexString(SHA256.HashData("tampered"u8.ToArray())).ToLowerInvariant();

        Assert.NotEqual(hash1, hash2);
    }

    // -------------------------------------------------------------------------
    // Sidecar file format (file I/O correctness)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HashVerification_SidecarFile_ContainsMatchingHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"HashTest_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var modelPath = Path.Combine(tempDir, "model.onnx");
            var hashPath = modelPath + ".sha256";

            var modelContent = "fake model content"u8.ToArray();
            await File.WriteAllBytesAsync(modelPath, modelContent);

            var expectedHash = Convert.ToHexString(SHA256.HashData(modelContent)).ToLowerInvariant();
            await File.WriteAllTextAsync(hashPath, expectedHash);

            // Sidecar can be read and its value matches a fresh hash of the file
            var storedHash = (await File.ReadAllTextAsync(hashPath)).Trim();
            Assert.Equal(expectedHash, storedHash);

            using var stream = File.OpenRead(modelPath);
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
            Assert.Equal(storedHash, actualHash);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task HashVerification_TamperedFile_HashMismatchIsDetectable()
    {
        // Documents that a tampered cached model file produces a detectable hash mismatch,
        // which is the signal SEC-001 uses to trigger re-download.
        var tempDir = Path.Combine(Path.GetTempPath(), $"HashTest_{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var modelPath = Path.Combine(tempDir, "model.onnx");
            var hashPath = modelPath + ".sha256";

            var originalContent = "original model content"u8.ToArray();
            await File.WriteAllBytesAsync(modelPath, originalContent);

            var correctHash = Convert.ToHexString(SHA256.HashData(originalContent)).ToLowerInvariant();
            await File.WriteAllTextAsync(hashPath, correctHash);

            // Simulate tampering
            await File.WriteAllBytesAsync(modelPath, "tampered content"u8.ToArray());

            var storedHash = (await File.ReadAllTextAsync(hashPath)).Trim();
            using var stream = File.OpenRead(modelPath);
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();

            Assert.NotEqual(storedHash, actualHash);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // LocalEmbeddingsOptions.ExpectedHash property (SEC-001)
    // -------------------------------------------------------------------------

    [Fact]
    public void HashVerification_ExpectedHash_DefaultIsNull()
    {
        var options = new LocalEmbeddingsOptions();
        Assert.Null(options.ExpectedHash);
    }

    [Fact]
    public void HashVerification_ExpectedHash_CanBeSet()
    {
        const string hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var options = new LocalEmbeddingsOptions { ExpectedHash = hash };

        Assert.Equal(hash, options.ExpectedHash);
    }

    [Fact]
    public void HashVerification_NoExpectedHash_BackwardCompatible()
    {
        // Ensure existing configuration without ExpectedHash still constructs fine
        var options = new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2",
            ExpectedHash = null
        };

        Assert.Null(options.ExpectedHash);
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", options.ModelName);
    }

    // -------------------------------------------------------------------------
    // EnsureModelAsync — ExpectedHash mismatch throws InvalidOperationException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HashVerification_ExpectedHashMismatch_ThrowsInvalidOperationException()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"HashTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            Directory.CreateDirectory(modelDir);

            // Simulate a freshly downloaded model file
            var modelContent = "fake model bytes"u8.ToArray();
            var modelPath = Path.Combine(modelDir, "model.onnx");
            await File.WriteAllBytesAsync(modelPath, modelContent);

            // Tokenizer files so the downloader considers the model fully present
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            // Supply a deliberately wrong expected hash (all zeros)
            const string wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => downloader.EnsureModelAsync("test/model", expectedHash: wrongHash));
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task HashVerification_CorrectExpectedHash_DoesNotThrow()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"HashTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            Directory.CreateDirectory(modelDir);

            var modelContent = "fake model bytes"u8.ToArray();
            var modelPath = Path.Combine(modelDir, "model.onnx");
            await File.WriteAllBytesAsync(modelPath, modelContent);

            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            // Compute the actual hash of the fake model file
            var correctHash = Convert.ToHexString(SHA256.HashData(modelContent)).ToLowerInvariant();

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            // Providing the correct hash must not throw
            var result = await downloader.EnsureModelAsync("test/model", expectedHash: correctHash);
            Assert.Equal(modelDir, result);
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task HashVerification_TamperedCachedFile_TriggersRedownload()
    {
        // SEC-001: a cached model file whose sidecar hash no longer matches
        // must be deleted and re-downloaded on the next EnsureModelAsync call.
        var cacheDir = Path.Combine(Path.GetTempPath(), $"HashTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            Directory.CreateDirectory(modelDir);

            var originalContent = "original model bytes"u8.ToArray();
            var modelPath = Path.Combine(modelDir, "model.onnx");
            await File.WriteAllBytesAsync(modelPath, originalContent);

            // Write the sidecar with the correct hash
            var correctHash = Convert.ToHexString(SHA256.HashData(originalContent)).ToLowerInvariant();
            await File.WriteAllTextAsync(modelPath + ".sha256", correctHash);

            // Now tamper with the model file to simulate a corrupt cache
            await File.WriteAllBytesAsync(modelPath, "corrupted bytes"u8.ToArray());

            // Track whether HTTP was called (re-download attempted)
            var httpWasCalled = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage _, CancellationToken _) =>
                {
                    httpWasCalled = true;
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            // The tampered file should be evicted and a download attempt made.
            // The download will fail (404), so we expect InvalidOperationException from the missing file.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => downloader.EnsureModelAsync("test/model"));

            Assert.True(httpWasCalled, "SEC-001: tampered cached model must trigger a re-download attempt.");
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
        }
    }
}
