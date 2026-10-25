using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;

namespace ElBruno.LocalEmbeddings.Tests;

public class ModelDownloadProcessTests
{
    private const string WorkerDirectoryVariable = "LOCALEMBEDDINGS_DOWNLOAD_TEST_WORKER_DIRECTORY";
    private const string WorkerIdVariable = "LOCALEMBEDDINGS_DOWNLOAD_TEST_WORKER_ID";

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 5)]
    public async Task EnsureModelAsync_AcrossProcesses_PreservesModelAndSidecar(bool warmCache, int processCount)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"EmbeddingsProcessTest_{Guid.NewGuid():N}");
        var cacheDirectory = Path.Combine(directory, "cache");
        var modelDirectory = Path.Combine(cacheDirectory, "test_model");
        var modelPath = Path.Combine(modelDirectory, "model.onnx");
        var sidecarPath = modelPath + ".sha256";
        var workers = new List<(Process Process, Task<string> Output, Task<string> Error)>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            Directory.CreateDirectory(directory);
            DateTime? sidecarWriteTime = null;
            if (warmCache)
            {
                using var client = new HttpClient(new ModelHandler());
                await new ModelDownloader(client, cacheDirectory).EnsureModelAsync("test/model");
                sidecarWriteTime = File.GetLastWriteTimeUtc(sidecarPath);
            }

            for (int i = 0; i < processCount; i++)
            {
                // Run only the worker test in a separate test host, never this coordinating test.
                var startInfo = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add("vstest");
                startInfo.ArgumentList.Add(typeof(ModelDownloadProcessTests).Assembly.Location);
                startInfo.ArgumentList.Add($"/TestCaseFilter:FullyQualifiedName={typeof(ModelDownloadProcessTests).FullName}.{nameof(DownloadWorker)}");
                startInfo.ArgumentList.Add("/logger:console;verbosity=minimal");
                startInfo.Environment[WorkerDirectoryVariable] = directory;
                startInfo.Environment[WorkerIdVariable] = i.ToString(CultureInfo.InvariantCulture);
                var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start download test worker.");
                workers.Add((process, process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync()));
            }

            while (Directory.GetFiles(directory, "*.ready").Length < processCount)
            {
                foreach (var worker in workers)
                {
                    if (worker.Process.HasExited)
                    {
                        Assert.Fail($"Worker exited before the start signal: {await worker.Output}\n{await worker.Error}");
                    }
                }

                await Task.Delay(50, timeout.Token);
            }

            await File.WriteAllTextAsync(Path.Combine(directory, "start"), "", timeout.Token);
            foreach (var worker in workers)
            {
                await worker.Process.WaitForExitAsync(timeout.Token);
                Assert.True(worker.Process.ExitCode == 0, $"{await worker.Output}\n{await worker.Error}");
            }

            var results = Directory.GetFiles(directory, "*.result");
            Assert.Equal(processCount, results.Length);
            int downloadingProcesses = results.Count(path => File.ReadAllText(path) != "0");
            Assert.Equal(warmCache ? 0 : 1, downloadingProcesses);
            string expectedHash = Convert.ToHexString(SHA256.HashData("fake model"u8.ToArray())).ToLowerInvariant();
            Assert.Equal(expectedHash, await File.ReadAllTextAsync(sidecarPath));
            Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(modelPath))).ToLowerInvariant());
            Assert.Empty(Directory.GetFiles(cacheDirectory, "*.tmp", SearchOption.AllDirectories));
            if (sidecarWriteTime.HasValue)
            {
                Assert.Equal(sidecarWriteTime.Value, File.GetLastWriteTimeUtc(sidecarPath));
            }
        }
        finally
        {
            foreach (var worker in workers)
            {
                if (!worker.Process.HasExited)
                {
                    worker.Process.Kill(entireProcessTree: true);
                }

                await worker.Process.WaitForExitAsync();
                worker.Process.Dispose();
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadWorker()
    {
        var directory = Environment.GetEnvironmentVariable(WorkerDirectoryVariable);
        if (directory is null)
        {
            return;
        }

        var id = Environment.GetEnvironmentVariable(WorkerIdVariable);
        Assert.False(string.IsNullOrEmpty(id));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await File.WriteAllTextAsync(Path.Combine(directory, id + ".ready"), "", timeout.Token);
        while (!File.Exists(Path.Combine(directory, "start")))
        {
            await Task.Delay(50, timeout.Token);
        }

        var handler = new ModelHandler();
        using var client = new HttpClient(handler);
        var downloader = new ModelDownloader(client, Path.Combine(directory, "cache"));
        await downloader.EnsureModelAsync("test/model", cancellationToken: timeout.Token);
        await File.WriteAllTextAsync(
            Path.Combine(directory, id + ".result"),
            handler.Requests.ToString(CultureInfo.InvariantCulture), timeout.Token);
    }

    private sealed class ModelHandler : HttpMessageHandler
    {
        private int _requests;

        public int Requests => _requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            await Task.Delay(100, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fake model") };
        }
    }
}
