using System.Net;
using ElBruno.LocalEmbeddings.Harrier.Options;
using Moq;
using Moq.Protected;

namespace ElBruno.LocalEmbeddings.Harrier.Tests;

public class HarrierModelDownloaderTests
{
    [Theory]
    [InlineData(HarrierModelVariant.Fp32, "model.onnx")]
    [InlineData(HarrierModelVariant.Fp16, "model_fp16.onnx")]
    [InlineData(HarrierModelVariant.Quantized, "model_quantized.onnx")]
    [InlineData(HarrierModelVariant.Q4, "model_q4.onnx")]
    public void GetOnnxFileName_ReturnsCorrectName(HarrierModelVariant variant, string expectedFileName)
    {
        var fileName = HarrierModelDownloader.GetOnnxFileName(variant);
        Assert.Equal(expectedFileName, fileName);
    }

    [Fact]
    public void ResolveModelPath_ThrowsWhenNoModelFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            Assert.Throws<FileNotFoundException>(
                () => HarrierModelDownloader.ResolveModelPath(tempDir, HarrierModelVariant.Quantized));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveModelPath_FindsVariantFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var expectedPath = Path.Combine(tempDir, "model_quantized.onnx");
            File.WriteAllText(expectedPath, "dummy");

            var result = HarrierModelDownloader.ResolveModelPath(tempDir, HarrierModelVariant.Quantized);
            Assert.Equal(expectedPath, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveModelPath_FallsBackToDefaultModel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var defaultPath = Path.Combine(tempDir, "model.onnx");
            File.WriteAllText(defaultPath, "dummy");

            // Request Q4 but only model.onnx exists — should fall back
            var result = HarrierModelDownloader.ResolveModelPath(tempDir, HarrierModelVariant.Q4);
            Assert.Equal(defaultPath, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new HarrierModelDownloader(null!));
    }

    [Fact]
    public void Constructor_ThrowsOnNullHttpClient()
    {
        var options = new HarrierEmbeddingsOptions();
        Assert.Throws<ArgumentNullException>(() => new HarrierModelDownloader(null!, options));
    }

    [Fact]
    public async Task EnsureModelAsync_WhenAnotherProcessIsDownloadingTheSameModel_WaitsAndUsesItsFiles()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new HarrierEmbeddingsOptions { CacheDirectory = cacheDir };
        var modelDir = Path.Combine(cacheDir, "onnx-community_harrier-oss-v1-270m-ONNX");
        var onnxFileName = HarrierModelDownloader.GetOnnxFileName(options.ModelVariant);

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

            var downloader = new HarrierModelDownloader(new HttpClient(mockHandler.Object), options);

            // Another process is downloading the model: it holds the lock of the model directory and the .tmp file it writes.
            Directory.CreateDirectory(Path.Combine(modelDir, "onnx"));
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var otherProcessTmp = new FileStream(Path.Combine(modelDir, "onnx", onnxFileName + ".tmp"), FileMode.Create, FileAccess.Write, FileShare.None);
            var ensureTask = downloader.EnsureModelAsync();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(ensureTask.IsCompleted, $"EnsureModelAsync should wait while another process downloads the same model, but it ended: {ensureTask.Exception?.InnerException}");

                // The other process finishes: the model files are in place, then it releases its files.
                await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName), "fake model");
                await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName + "_data"), "fake weights");
                await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
                otherProcessTmp.Dispose();
                File.Delete(Path.Combine(modelDir, "onnx", onnxFileName + ".tmp"));
                otherProcessLock.Dispose();

                var resultPath = await ensureTask;

                Assert.Equal(modelDir, resultPath);
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
            {
                Directory.Delete(cacheDir, true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WithCachedModelAndCancelledToken_ThrowsTaskCanceledException()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new HarrierEmbeddingsOptions { CacheDirectory = cacheDir };
        var modelDir = Path.Combine(cacheDir, "onnx-community_harrier-oss-v1-270m-ONNX");
        var onnxFileName = HarrierModelDownloader.GetOnnxFileName(options.ModelVariant);

        try
        {
            // A cached model, without sidecar hashes as a cache copied by hand.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName), "fake model");
            await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName + "_data"), "fake weights");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");

            var downloader = new HarrierModelDownloader(new HttpClient(new Mock<HttpMessageHandler>().Object), options);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // A call that is already cancelled ends with the cancellation, also when the model is complete.
            await Assert.ThrowsAsync<TaskCanceledException>(() => downloader.EnsureModelAsync(cancellationToken: cts.Token));
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
    public async Task EnsureModelAsync_WhenItDownloadsAfterWaitingForTheLock_PostsTheProgressToTheContextOfTheCaller()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new HarrierEmbeddingsOptions { CacheDirectory = cacheDir };
        var modelDir = Path.Combine(cacheDir, "onnx-community_harrier-oss-v1-270m-ONNX");
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
            var downloader = new HarrierModelDownloader(new HttpClient(mockHandler.Object), options);
            var progress = new RecordingProgress();
            var callerContext = new CallerContext();

            // Another process holds the lock, then stops without the model: this call waits, then downloads the model itself.
            Directory.CreateDirectory(modelDir);
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var ensureTask = callerContext.Run(() => downloader.EnsureModelAsync(progress: progress));

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

    [Fact]
    public async Task EnsureModelAsync_WithModelWithoutSidecarHashes_ReturnsWithoutWaitingForTheLock()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new HarrierEmbeddingsOptions { CacheDirectory = cacheDir };
        var modelDir = Path.Combine(cacheDir, "onnx-community_harrier-oss-v1-270m-ONNX");
        var onnxFileName = HarrierModelDownloader.GetOnnxFileName(options.ModelVariant);

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

            var downloader = new HarrierModelDownloader(new HttpClient(mockHandler.Object), options);

            // A cache without sidecar hashes, for example copied by hand, is accepted as before, also while another process holds the lock
            // and also when it cannot be written.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName), "fake model");
            await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName + "_data"), "fake weights");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            using (new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var resultPath = await downloader.EnsureModelAsync().WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Equal(modelDir, resultPath);
                Assert.False(httpRequested, "HTTP should not be called when the model files are there");
            }
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
            }
        }
    }

    [Fact]
    public async Task EnsureModelAsync_WhenTheModelIsGoneButItsSidecarHashIsThere_WaitsForTheLock()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new HarrierEmbeddingsOptions { CacheDirectory = cacheDir };
        var modelDir = Path.Combine(cacheDir, "onnx-community_harrier-oss-v1-270m-ONNX");
        var onnxFileName = HarrierModelDownloader.GetOnnxFileName(options.ModelVariant);

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

            var downloader = new HarrierModelDownloader(new HttpClient(mockHandler.Object), options);

            // Another process holds the lock while it deletes an invalid model: the model is gone, its sidecar hash is still there.
            // The check without the lock cannot read the model, so the call waits for the lock.
            Directory.CreateDirectory(modelDir);
            await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName + ".sha256"), "0000");
            await File.WriteAllTextAsync(Path.Combine(modelDir, onnxFileName + "_data"), "fake weights");
            await File.WriteAllTextAsync(Path.Combine(modelDir, "tokenizer.json"), "{}");
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var ensureTask = downloader.EnsureModelAsync();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(ensureTask.IsCompleted, $"EnsureModelAsync should wait for the lock while the model is gone, but it ended: {ensureTask.Exception?.InnerException}");

                otherProcessLock.Dispose();

                Assert.Equal(modelDir, await ensureTask);
                Assert.True(File.Exists(Path.Combine(modelDir, onnxFileName)));
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
                Directory.Delete(cacheDir, true);
            }
        }
    }

    // Records the synchronization context of every report.
    private sealed class RecordingProgress : IProgress<double>
    {
        public System.Collections.Concurrent.ConcurrentQueue<SynchronizationContext?> Contexts { get; } = new();

        public void Report(double value) => Contexts.Enqueue(SynchronizationContext.Current);
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
