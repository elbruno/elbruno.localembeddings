using System.Net;
using ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader;
using Moq;
using Moq.Protected;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

public class HuggingFaceImageModelDownloaderTests
{
    [Fact]
    public async Task EnsureModelDownloadedAsync_WhenAnotherProcessIsDownloadingTheModel_WaitsBeforeDownloading()
    {
        var modelDir = Path.Combine(Path.GetTempPath(), $"ImageEmbeddingsTest_{Guid.NewGuid()}");

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

            var downloader = new HuggingFaceImageModelDownloader(new HttpClient(mockHandler.Object));

            // Another process is downloading the model: it holds the lock of the model directory and the .tmp file it writes.
            Directory.CreateDirectory(Path.Combine(modelDir, "onnx"));
            var otherProcessLock = new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var otherProcessTmp = new FileStream(Path.Combine(modelDir, "onnx", "text_model.onnx.tmp"), FileMode.Create, FileAccess.Write, FileShare.None);
            var ensureTask = downloader.EnsureModelDownloadedAsync(modelDir);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(ensureTask.IsCompleted, $"EnsureModelDownloadedAsync should wait while another process downloads the model, but it ended: {ensureTask.Exception?.InnerException}");
                Assert.False(httpRequested, "HTTP should not be called while another process downloads the model");

                // The other process stops without the files, and releases them: this one downloads the model.
                otherProcessTmp.Dispose();
                File.Delete(Path.Combine(modelDir, "onnx", "text_model.onnx.tmp"));
                otherProcessLock.Dispose();

                await ensureTask;

                Assert.True(File.Exists(Path.Combine(modelDir, "text_model.onnx")));
                Assert.True(File.Exists(Path.Combine(modelDir, "vision_model.onnx")));
            }
            finally
            {
                // Released and finished also when an assertion fails, so that the model directory can be deleted.
                otherProcessTmp.Dispose();
                otherProcessLock.Dispose();
                await Task.WhenAny(ensureTask, Task.Delay(TimeSpan.FromSeconds(10)));
            }
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
    public async Task EnsureModelDownloadedAsync_TwoCallsInTheSameProcess_SecondWaitsForTheFirst()
    {
        var modelDir = Path.Combine(Path.GetTempPath(), $"ImageEmbeddingsTest_{Guid.NewGuid()}");

        try
        {
            // The first GET of text_model.onnx waits until the test releases it, so the first call stays in its download.
            var firstGetStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstGet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var textModelGets = 0;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage req, CancellationToken _) =>
                {
                    if (req.Method == HttpMethod.Get
                        && req.RequestUri!.AbsolutePath.EndsWith("/onnx/text_model.onnx", StringComparison.Ordinal)
                        && Interlocked.Increment(ref textModelGets) == 1)
                    {
                        firstGetStarted.SetResult();
                        await releaseFirstGet.Task;
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") };
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var firstTask = new HuggingFaceImageModelDownloader(httpClient).EnsureModelDownloadedAsync(modelDir);
            Task? secondTask = null;

            try
            {
                await firstGetStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                secondTask = new HuggingFaceImageModelDownloader(httpClient).EnsureModelDownloadedAsync(modelDir);

                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(secondTask.IsCompleted, $"The second call should wait while the first one downloads the model, but it ended: {secondTask.Exception?.InnerException}");

                releaseFirstGet.SetResult();
                await firstTask;
                await secondTask;

                Assert.True(File.Exists(Path.Combine(modelDir, "text_model.onnx")));
                Assert.True(File.Exists(Path.Combine(modelDir, "vision_model.onnx")));
            }
            finally
            {
                // Finished also when an assertion fails, so that the model directory can be deleted.
                releaseFirstGet.TrySetResult();
                await Task.WhenAny(Task.WhenAll(firstTask, secondTask ?? Task.CompletedTask), Task.Delay(TimeSpan.FromSeconds(10)));
            }
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
    public async Task EnsureModelDownloadedAsync_WhenCancelledWhileAnotherProcessHoldsTheLock_ThrowsInvalidOperationException()
    {
        var modelDir = Path.Combine(Path.GetTempPath(), $"ImageEmbeddingsTest_{Guid.NewGuid()}");

        try
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") });

            var downloader = new HuggingFaceImageModelDownloader(new HttpClient(mockHandler.Object));

            // Another process holds the lock of the model directory while it downloads the model.
            Directory.CreateDirectory(modelDir);
            using (new FileStream(Path.Combine(modelDir, ".download.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                // As a cancellation during the download, it is reported as a failed download.
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.EnsureModelDownloadedAsync(modelDir, cts.Token));
                Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
            }
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
    public async Task EnsureModelDownloadedAsync_WithCompleteModelAndCancelledToken_ThrowsInvalidOperationException()
    {
        var modelDir = Path.Combine(Path.GetTempPath(), $"ImageEmbeddingsTest_{Guid.NewGuid()}");

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

            var downloader = new HuggingFaceImageModelDownloader(new HttpClient(mockHandler.Object));
            await downloader.EnsureModelDownloadedAsync(modelDir);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // As a cancellation during the download, a call that is already cancelled is reported as a failed download, also when the
            // model is complete.
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.EnsureModelDownloadedAsync(modelDir, cts.Token));
            Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
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
    public async Task EnsureModelDownloadedAsync_WhenTheLockPathIsADirectory_ThrowsInvalidOperationException()
    {
        var modelDir = Path.Combine(Path.GetTempPath(), $"ImageEmbeddingsTest_{Guid.NewGuid()}");
        var lockPath = Path.Combine(modelDir, ".download.lock");

        try
        {
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
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") };
                });
            var downloader = new HuggingFaceImageModelDownloader(new HttpClient(mockHandler.Object));

            // A directory where the lock file should be: the call fails on it, before any request.
            Directory.CreateDirectory(lockPath);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.EnsureModelDownloadedAsync(modelDir));
            Assert.IsType<UnauthorizedAccessException>(exception.InnerException);
            Assert.Contains(lockPath, exception.InnerException!.Message);
            Assert.False(requested, "HTTP should not be called when the lock path is a directory");
        }
        finally
        {
            if (Directory.Exists(modelDir))
            {
                Directory.Delete(modelDir, recursive: true);
            }
        }
    }
}
