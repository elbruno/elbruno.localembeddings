using System.Net;
using Moq;
using Moq.Protected;

namespace elbruno.LocalEmbeddings.Tests;

public class ModelDownloaderTests
{
    [Fact]
    public void GetCacheDirectory_ReturnsPlatformAppropiatePath()
    {
        var downloader = new ModelDownloader();
        var cacheDir = downloader.GetCacheDirectory();

        Assert.False(string.IsNullOrWhiteSpace(cacheDir));

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("LocalEmbeddings", cacheDir);
            Assert.Contains("models", cacheDir);
            // Should be under LocalAppData on Windows
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                Assert.StartsWith(localAppData, cacheDir);
            }
        }
        else
        {
            // Linux/macOS: Should be under ~/.local/share or XDG_DATA_HOME
            Assert.Contains("LocalEmbeddings", cacheDir);
            Assert.Contains("models", cacheDir);
        }
    }

    [Fact]
    public void GetCacheDirectory_WithCustomDirectory_ReturnsCustomPath()
    {
        var customPath = Path.Combine(Path.GetTempPath(), "CustomEmbeddingsCache");
        var httpClient = new HttpClient();
        var downloader = new ModelDownloader(httpClient, customPath);

        Assert.Equal(customPath, downloader.GetCacheDirectory());
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ModelDownloader(null!));
    }

    [Fact]
    public async Task EnsureModelAsync_WithNullOrEmptyModelName_ThrowsArgumentException()
    {
        var downloader = new ModelDownloader();

        await Assert.ThrowsAsync<ArgumentException>(() => downloader.EnsureModelAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => downloader.EnsureModelAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => downloader.EnsureModelAsync("   "));
    }

    [Fact]
    public async Task EnsureModelAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        try
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var downloader = new ModelDownloader(new HttpClient(), cacheDir);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => downloader.EnsureModelAsync("test-model", cancellationToken: cts.Token));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WhenModelExists_DoesNotRedownload()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");
        var modelPath = Path.Combine(modelDir, "model.onnx");

        try
        {
            // Create the model directory and all expected files
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(modelPath, "fake model content");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            // Track if HTTP was called for model.onnx (the critical file)
            var modelOnnxRequested = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                {
                    if (req.RequestUri?.ToString().Contains("model.onnx") == true)
                    {
                        modelOnnxRequested = true;
                    }
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            var resultPath = await downloader.EnsureModelAsync("test/model");

            Assert.Equal(modelDir, resultPath);
            Assert.False(modelOnnxRequested, "HTTP should not be called for model.onnx when it exists");
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithMockedHttpClient_DownloadsModel()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");

        try
        {
            var fakeModelContent = new byte[] { 0x4F, 0x4E, 0x4E, 0x58 }; // Fake ONNX header

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("model.onnx")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fakeModelContent)
                });

            // Return 404 for tokenizer files (they're optional)
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => !r.RequestUri!.ToString().Contains("model.onnx")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            var resultPath = await downloader.EnsureModelAsync("test/model");

            Assert.True(Directory.Exists(resultPath));
            var modelFile = Path.Combine(resultPath, "model.onnx");
            Assert.True(File.Exists(modelFile));
            Assert.Equal(fakeModelContent, await File.ReadAllBytesAsync(modelFile));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WhenDownloadFails_ThrowsInvalidOperationException()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");

        try
        {
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
                () => downloader.EnsureModelAsync("nonexistent/model"));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_ReportsProgress()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");

        try
        {
            var fakeModelContent = new byte[1024];
            var progressValues = new List<double>();

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("model.onnx")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(fakeModelContent)
                    };
                    response.Content.Headers.ContentLength = fakeModelContent.Length;
                    return response;
                });

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => !r.RequestUri!.ToString().Contains("model.onnx")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);
            var progress = new Progress<double>(p => progressValues.Add(p));

            await downloader.EnsureModelAsync("test/model", progress);

            // Should have reported some progress
            Assert.NotEmpty(progressValues);
            // Final progress should be 1.0 or close to it
            Assert.Contains(progressValues, p => p >= 0.9);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void DefaultModelName_ReturnsExpectedValue()
    {
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", ModelDownloader.DefaultModelName);
    }
}
