using System.Net;
using Moq;
using Moq.Protected;

namespace ElBruno.LocalEmbeddings.Npu.Intel.Tests;

public class IntelModelDownloaderTests
{
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

            var downloader = new IntelModelDownloader(new HttpClient(mockHandler.Object), cacheDir);

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
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
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

            var downloader = new IntelModelDownloader(new HttpClient(mockHandler.Object), cacheDir);
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

            var downloader = new IntelModelDownloader(new HttpClient(mockHandler.Object), cacheDir);

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

            var downloader = new IntelModelDownloader(new HttpClient(new Mock<HttpMessageHandler>().Object), cacheDir);
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
            var downloader = new IntelModelDownloader(new HttpClient(mockHandler.Object), cacheDir);
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
