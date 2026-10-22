using System.Net;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Moq;
using Moq.Protected;

namespace ElBruno.LocalEmbeddings.Tests;

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
    public async Task EnsureModelAsync_WithExistingDefaultModel_ReturnsDirectoryWithoutDownload()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake model");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            var httpRequested = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                {
                    httpRequested = true;
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            var resultPath = await downloader.EnsureModelAsync("test/model");

            Assert.Equal(modelDir, resultPath);
            Assert.True(File.Exists(Path.Combine(resultPath, "model.onnx")));
            Assert.False(httpRequested, "HTTP should not be called when all files exist locally");
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WhenAnotherProcessIsDownloadingTheSameModel_WaitsAndUsesItsFiles()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            var httpRequested = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                {
                    httpRequested = true;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") };
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            // Another process is downloading the model: it holds the lock of the model directory and the .tmp file it writes.
            Directory.CreateDirectory(Path.Combine(modelDir, "onnx"));
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var otherProcessTmp = new FileStream(Path.Combine(modelDir, "onnx", "model.onnx.tmp"), FileMode.Create, FileAccess.Write, FileShare.None);
            var ensureTask = downloader.EnsureModelAsync("test/model");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(ensureTask.IsCompleted, $"EnsureModelAsync should wait while another process downloads the same model, but it ended: {ensureTask.Exception?.InnerException}");

                // The other process finishes: the model files are in place, then it releases its files.
                await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake model");
                await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
                await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
                await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");
                otherProcessTmp.Dispose();
                File.Delete(Path.Combine(modelDir, "onnx", "model.onnx.tmp"));
                otherProcessLock.Dispose();

                var resultPath = await ensureTask;

                Assert.Equal(modelDir, resultPath);
                Assert.True(File.Exists(Path.Combine(resultPath, "model.onnx")));
                Assert.False(httpRequested, "HTTP should not be called when another process downloaded the model");
            }
            finally
            {
                // Released and finished also when an assertion fails, so that the cache directory can be deleted.
                otherProcessTmp.Dispose();
                otherProcessLock.Dispose();
                await Task.WhenAny(ensureTask, Task.Delay(TimeSpan.FromSeconds(10)));
            }
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithVerifiedModelWhileAnotherProcessHoldsTheLock_ReturnsWithoutWaiting()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            // A complete model with its sidecar hash, as a previous download leaves it.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake model");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("fake model"u8.ToArray())).ToLowerInvariant();
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx.sha256"), hash);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            var httpRequested = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                {
                    httpRequested = true;
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            // Another process holds the lock of the model directory, for example while it checks the same model.
            using var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            var resultPath = await downloader.EnsureModelAsync("test/model").WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(modelDir, resultPath);
            Assert.False(httpRequested, "HTTP should not be called when the model is complete and verified");
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WhenCancelledWhileAnotherProcessHoldsTheLock_ThrowsAndLaterCallsSucceed()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            Directory.CreateDirectory(modelDir);
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => downloader.EnsureModelAsync("test/model", cancellationToken: cts.Token));
            }
            finally
            {
                otherProcessLock.Dispose();
            }

            var resultPath = await downloader.EnsureModelAsync("test/model").WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(modelDir, resultPath);
            Assert.True(File.Exists(Path.Combine(resultPath, "model.onnx")));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WhenDownloadFails_ReleasesTheLockOfTheModelDirectory()
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

            await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.EnsureModelAsync("nonexistent/model"));

            // Another process can take the lock right away.
            using var otherProcessLock = new FileStream(
                Path.Combine(cacheDir, "nonexistent_model", ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
    public async Task EnsureModelAsync_WithPreferQuantized_WhenQuantizedModelExistsLocally_ReturnsDirectory()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model_quantized.onnx"), "fake quantized model");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            var httpRequested = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                {
                    httpRequested = true;
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            var resultPath = await downloader.EnsureModelAsync("test/model", preferQuantized: true);

            Assert.Equal(modelDir, resultPath);
            Assert.True(File.Exists(Path.Combine(resultPath, "model_quantized.onnx")));
            Assert.False(httpRequested, "HTTP should not be called when quantized model exists locally");
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithPreferQuantized_WhenOnlyDefaultModelExists_ReturnsDirectory()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake default model");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            var httpRequested = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                {
                    httpRequested = true;
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            // Even with preferQuantized, should succeed if default model exists
            var resultPath = await downloader.EnsureModelAsync("test/model", preferQuantized: true);

            Assert.Equal(modelDir, resultPath);
            Assert.True(File.Exists(Path.Combine(resultPath, "model.onnx")));
            Assert.False(httpRequested, "HTTP should not be called for model files when default model exists locally");
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithPreferQuantized_WhenInt8ModelExists_ReturnsDirectory()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model_int8.onnx"), "fake int8 model");

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage _, CancellationToken _) =>
                    new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(mockHandler.Object);
            var downloader = new ModelDownloader(httpClient, cacheDir);

            var resultPath = await downloader.EnsureModelAsync("test/model", preferQuantized: true);

            Assert.Equal(modelDir, resultPath);
            Assert.True(File.Exists(Path.Combine(resultPath, "model_int8.onnx")));
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

    [Fact]
    public async Task EnsureModelAsync_WithVerifiedModel_ReportsCompleteProgress()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            // A complete model with its sidecar hash, as a previous download leaves it.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake model");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("fake model"u8.ToArray())).ToLowerInvariant();
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx.sha256"), hash);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.NotFound));

            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);
            var progress = new RecordingProgress();
            var callerContext = new CallerContext();

            await callerContext.Run(() => downloader.EnsureModelAsync("test/model", progress: progress));

            // As when the downloader finds every file, the end of the progress is posted to the context of the caller.
            Assert.False(progress.Completed.IsCompleted);
            callerContext.RunPosted();
            Assert.Same(callerContext, await progress.Completed);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WhenAnotherProcessFinishesTheModel_ReportsCompleteProgress()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.NotFound));

            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);
            var progress = new RecordingProgress();
            var callerContext = new CallerContext();

            // Another process holds the lock while it downloads the model, then finishes it with its sidecar hash and releases the lock.
            Directory.CreateDirectory(modelDir);
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var ensureTask = callerContext.Run(() => downloader.EnsureModelAsync("test/model", progress: progress));

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Directory.CreateDirectory(modelDir);
                await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake model");
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("fake model"u8.ToArray())).ToLowerInvariant();
                await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx.sha256"), hash);
                await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
                await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
                await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");
                otherProcessLock.Dispose();

                Assert.Equal(modelDir, await ensureTask);

                // The end of the progress is posted to the context of the caller, also after the wait for the lock.
                Assert.False(progress.Completed.IsCompleted);
                callerContext.RunPosted();
                Assert.Same(callerContext, await progress.Completed);
            }
            finally
            {
                // Released and finished also when an assertion fails, so that the cache directory can be deleted.
                otherProcessLock.Dispose();
                await Task.WhenAny(ensureTask, Task.Delay(TimeSpan.FromSeconds(10)));
            }
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithReadOnlyLockFile_DownloadsTheModel()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");
        var lockPath = Path.Combine(modelDir, ".download.lock");

        try
        {
            // A lock file that this process cannot write, for example created by another user with umask 022 in a shared cache.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(lockPath, "");
            File.SetAttributes(lockPath, FileAttributes.ReadOnly);

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") });

            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);

            var resultPath = await downloader.EnsureModelAsync("test/model");

            Assert.Equal(modelDir, resultPath);
            Assert.True(File.Exists(Path.Combine(modelDir, "model.onnx")));
        }
        finally
        {
            if (File.Exists(lockPath))
            {
                File.SetAttributes(lockPath, FileAttributes.Normal);
            }

            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AcquireAsync_WhenAnotherProcessHoldsTheLockPastTheTimeLimit_ThrowsTimeoutExceptionThatNamesTheLockFile()
    {
        var modelDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var lockPath = Path.Combine(modelDir, ".download.lock");

        try
        {
            // Another process holds the lock and does not release it, as a process that hangs.
            Directory.CreateDirectory(modelDir);
            using var otherProcessLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            var acquireTask = ModelDownloadLock.AcquireAsync(modelDir, TimeSpan.FromSeconds(1), CancellationToken.None);

            Assert.Same(acquireTask, await Task.WhenAny(acquireTask, Task.Delay(TimeSpan.FromSeconds(10))));
            var exception = await Assert.ThrowsAsync<TimeoutException>(() => acquireTask);
            Assert.Contains(lockPath, exception.Message);
        }
        finally
        {
            if (Directory.Exists(modelDir))
            {
                Directory.Delete(modelDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithReadOnlyCacheWithoutTheModel_FailsWithTheErrorOfTheDownload()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            // A model directory that this process cannot write, without the model and without a lock file.
            Directory.CreateDirectory(modelDir);
            if (!DenyCreation(modelDir))
            {
                // A privileged process on Unix creates files anyway: there the directory cannot be made read-only.
                return;
            }

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") });

            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => downloader.EnsureModelAsync("test/model"));

            // As without the lock, the error is the one of the first write of the download, not one of the lock file.
            Assert.DoesNotContain(".download.lock", exception.Message);
            Assert.False(File.Exists(Path.Combine(modelDir, ".download.lock")));
        }
        finally
        {
            if (Directory.Exists(modelDir))
            {
                AllowCreation(modelDir);
            }

            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithVerifiedModelWithoutUnusedTokenizerFiles_ReturnsWithoutWaitingForTheLock()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            // A complete model with its sidecar hash and vocab.txt, the only tokenizer file that the tokenizer reads, as a repository
            // without tokenizer.json and tokenizer_config.json leaves it.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake model");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("fake model"u8.ToArray())).ToLowerInvariant();
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx.sha256"), hash);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            var requested = false;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    requested = true;
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);

            // Another process holds the lock: the call returns without waiting for it and without any request.
            using (new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var resultPath = await downloader.EnsureModelAsync("test/model").WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Equal(modelDir, resultPath);
                Assert.False(requested, "HTTP should not be called when the model and vocab.txt are there");
            }
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithVerifiedModelAndCancelledToken_ThrowsTaskCanceledException()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");

        try
        {
            // A complete model with its sidecar hash, as a previous download leaves it.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx"), "fake model");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("fake model"u8.ToArray())).ToLowerInvariant();
            await File.WriteAllTextAsync(Path.Combine(modelDir, "model.onnx.sha256"), hash);
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer_config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "vocab.txt"), "test");

            var downloader = new ModelDownloader(new HttpClient(new Mock<HttpMessageHandler>().Object), cacheDir);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // A call that is already cancelled ends with the cancellation, also when the model is complete.
            await Assert.ThrowsAsync<TaskCanceledException>(() => downloader.EnsureModelAsync("test/model", cancellationToken: cts.Token));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithLockFileThatCannotBeOpened_DownloadsTheModel()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");
        var lockPath = Path.Combine(modelDir, ".download.lock");

        try
        {
            // A lock file that this process cannot open even for reading, for example created by another user with mode 0600.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(lockPath, "");
            if (!DenyOpening(lockPath))
            {
                // A privileged process on Unix opens it anyway.
                return;
            }

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") });

            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);

            var resultPath = await downloader.EnsureModelAsync("test/model");

            // As without the lock, the model is downloaded.
            Assert.Equal(modelDir, resultPath);
            Assert.True(File.Exists(Path.Combine(modelDir, "model.onnx")));
        }
        finally
        {
            if (File.Exists(lockPath))
            {
                AllowOpening(lockPath);
            }

            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    // Denies this process the opening of a file. False when it can open it anyway.
    private static bool DenyOpening(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            SetOpeningRule(file, deny: true);
        }
        else
        {
            File.SetUnixFileMode(file, UnixFileMode.None);
        }

        try
        {
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void AllowOpening(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            SetOpeningRule(file, deny: false);
        }
        else
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetOpeningRule(string file, bool deny)
    {
        var info = new FileInfo(file);
        var security = info.GetAccessControl();
        var rule = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.ReadData | FileSystemRights.WriteData,
            AccessControlType.Deny);
        if (deny)
        {
            security.AddAccessRule(rule);
        }
        else
        {
            security.RemoveAccessRule(rule);
        }

        info.SetAccessControl(security);
    }

    // Denies this process the creation of files and directories in a directory. False when it can create them anyway.
    private static bool DenyCreation(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            SetCreationRule(directory, deny: true);
        }
        else
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        try
        {
            var probe = Path.Combine(directory, "probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void AllowCreation(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            SetCreationRule(directory, deny: false);
        }
        else
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetCreationRule(string directory, bool deny)
    {
        var info = new DirectoryInfo(directory);
        var security = info.GetAccessControl();
        var rule = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories,
            AccessControlType.Deny);
        if (deny)
        {
            security.AddAccessRule(rule);
        }
        else
        {
            security.RemoveAccessRule(rule);
        }

        info.SetAccessControl(security);
    }

    [Fact]
    public async Task EnsureModelAsync_WhenItDownloadsAfterWaitingForTheLock_PostsTheProgressToTheContextOfTheCaller()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"EmbeddingsTest_{Guid.NewGuid()}");
        var modelDir = Path.Combine(cacheDir, "test_model");
        try
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") });
            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);
            var progress = new RecordingProgress();
            var callerContext = new CallerContext();

            // Another process holds the lock, then stops without the model: this call waits, then downloads the model itself.
            Directory.CreateDirectory(modelDir);
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var ensureTask = callerContext.Run(() => downloader.EnsureModelAsync("test/model", progress: progress));

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                otherProcessLock.Dispose();
                Assert.Equal(modelDir, await ensureTask);

                // The progress of the download is posted to the context of the caller, also after the wait for the lock.
                Assert.Empty(progress.Contexts);
                callerContext.RunPosted();
                Assert.NotEmpty(progress.Contexts);
                Assert.All(progress.Contexts, context => Assert.Same(callerContext, context));
            }
            finally
            {
                // Released and finished also when an assertion fails, so that the cache directory can be deleted.
                otherProcessLock.Dispose();
                await Task.WhenAny(ensureTask, Task.Delay(TimeSpan.FromSeconds(10)));
            }
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    // Records the synchronization context of every report, and of the report of the end of the progress.
    private sealed class RecordingProgress : IProgress<double>
    {
        private readonly TaskCompletionSource<SynchronizationContext?> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SynchronizationContext?> Completed => _completed.Task;

        public System.Collections.Concurrent.ConcurrentQueue<SynchronizationContext?> Contexts { get; } = new();

        public void Report(double value)
        {
            Contexts.Enqueue(SynchronizationContext.Current);
            if (value == 1.0)
            {
                _completed.TrySetResult(SynchronizationContext.Current);
            }
        }
    }

    // A synchronization context like the one of a UI thread: what is posted to it runs only when the test runs it, with this context as
    // the current one.
    private sealed class CallerContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        // Starts an operation with this context as the current one, as a caller on a UI thread does.
        public Task<T> Run<T>(Func<Task<T>> start)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                return start();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        public void RunPosted()
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                while (_posted.TryDequeue(out var item))
                {
                    item.Callback(item.State);
                }
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}
