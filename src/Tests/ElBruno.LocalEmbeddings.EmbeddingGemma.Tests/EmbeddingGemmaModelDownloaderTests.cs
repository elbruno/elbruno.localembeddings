using System.Net;

namespace ElBruno.LocalEmbeddings.EmbeddingGemma.Tests;

public class EmbeddingGemmaModelDownloaderTests
{
    [Fact]
    public async Task EnsureModelAsync_WhenAnotherProcessIsDownloadingTheModel_WaitsBeforeDownloading()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var modelDir = Path.Combine(cacheDir, "embeddinggemma-2");

        try
        {
            var handler = new RecordingHandler();
            var downloader = new EmbeddingGemmaModelDownloader(new HttpClient(handler), cacheDir);

            // Another process is downloading the model: it holds the lock of the model directory and the .tmp file it writes.
            Directory.CreateDirectory(Path.Combine(modelDir, "onnx"));
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var otherProcessTmp = new FileStream(Path.Combine(modelDir, "onnx", "model_q4.onnx.tmp"), FileMode.Create, FileAccess.Write, FileShare.None);
            var ensureTask = downloader.EnsureModelAsync();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(ensureTask.IsCompleted, $"EnsureModelAsync should wait while another process downloads the model, but it ended: {ensureTask.Exception?.InnerException}");
                Assert.False(handler.Requested, "HTTP should not be called while another process downloads the model");

                // The other process stops without the files: this one downloads them, and the fake content fails the pinned hashes.
                otherProcessTmp.Dispose();
                File.Delete(Path.Combine(modelDir, "onnx", "model_q4.onnx.tmp"));
                otherProcessLock.Dispose();

                await Assert.ThrowsAsync<InvalidDataException>(() => ensureTask);
                Assert.True(handler.Requested);
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

    [SkippableFact]
    public async Task EnsureModelAsync_WithCachedModelAndCancelledToken_ThrowsTaskCanceledException()
    {
        // The pinned hashes need the real model: the one in the default cache, when it is there.
        var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ElBruno", "LocalEmbeddings", "models");
        Skip.IfNot(
            EmbeddingGemmaModelDownloader.FilesAreValid(Path.Combine(cacheDir, "embeddinggemma-2")),
            "The EmbeddingGemma model is not in the default cache.");

        var downloader = new EmbeddingGemmaModelDownloader(new HttpClient(new RecordingHandler()), cacheDir);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // A call that is already cancelled ends with the cancellation, also when the model is complete.
        await Assert.ThrowsAsync<TaskCanceledException>(() => downloader.EnsureModelAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task EnsureModelAsync_WhenItDownloadsAfterWaitingForTheLock_PostsTheProgressToTheContextOfTheCaller()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var modelDir = Path.Combine(cacheDir, "embeddinggemma-2");

        try
        {
            var downloader = new EmbeddingGemmaModelDownloader(new HttpClient(new RecordingHandler()), cacheDir);
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

                // The fake content fails the pinned hashes after the download.
                await Assert.ThrowsAsync<InvalidDataException>(() => ensureTask);

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
                Directory.Delete(cacheDir, true);
            }
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public bool Requested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") });
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
