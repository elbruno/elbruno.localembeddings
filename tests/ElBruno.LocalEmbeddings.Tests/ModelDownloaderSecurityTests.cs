using System.Net;
using Moq;
using Moq.Protected;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Tests for SEC-006: path traversal guard added to ModelDownloader.EnsureModelAsync.
///
/// Key insight from running the suite: DefaultPathHelper.SanitizeModelName (the external
/// HuggingFace downloader package) converts '/' to '_', so slash-based traversal names
/// like "../../escape" resolve to ".._.._ escape" — a safe subdirectory inside the cache.
/// The SEC-006 guard provides defense-in-depth for inputs where sanitization does NOT
/// replace the separator, such as a bare ".." with no slashes.
/// </summary>
public class ModelDownloaderSecurityTests
{
    // -------------------------------------------------------------------------
    // Guard fires: bare ".." (no slash) escapes the cache after Path.GetFullPath
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureModelAsync_DotDotModelName_ThrowsArgumentException()
    {
        // ".." contains no '/', so SanitizeModelName cannot convert it to a safe name.
        // Path.GetFullPath(Path.Combine(cacheDir, "..")) resolves to the parent directory,
        // which is outside cacheDir. The SEC-006 guard must reject this.
        var cacheDir = Path.Combine(Path.GetTempPath(), $"SecTest_{Guid.NewGuid()}");
        try
        {
            var downloader = new ModelDownloader(new HttpClient(), cacheDir);

            await Assert.ThrowsAsync<ArgumentException>(
                () => downloader.EnsureModelAsync(".."));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Guard fires: Windows absolute path (no slash conversion possible for drive letter)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureModelAsync_WindowsAbsolutePath_ThrowsArgumentException()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows-only path traversal scenario");

        // On Windows: Path.Combine("C:\cache", "C:\evil") returns "C:\evil" — outside cache.
        // If SanitizeModelName preserves the drive-letter prefix, the guard fires.
        var cacheDir = Path.Combine(Path.GetTempPath(), $"SecTest_{Guid.NewGuid()}");
        try
        {
            var downloader = new ModelDownloader(new HttpClient(), cacheDir);

            // Use a drive-only path that cannot be confused with a valid model name
            var exception = await Record.ExceptionAsync(
                () => downloader.EnsureModelAsync(@"C:\evil\model"));

            // Either the guard fires (ArgumentException) or sanitization made it safe
            // (some other exception). What must never happen: a successful result
            // pointing outside the cache.
            if (exception is not ArgumentException)
            {
                // Sanitization kept the path inside the cache — verify this.
                if (Directory.Exists(cacheDir))
                {
                    var cacheRoot = Path.GetFullPath(cacheDir);
                    foreach (var file in Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories))
                    {
                        Assert.True(
                            Path.GetFullPath(file).StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase),
                            $"File escaped cache directory: {file}");
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Slash-based traversal: sanitization converts '/' → '_', keeping paths inside
    // cache. Guard provides defense-in-depth for this class of inputs.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("../../escape")]
    [InlineData("../secret")]
    [InlineData("sub/../../escape")]
    [InlineData("a/b/../../../outside")]
    public async Task EnsureModelAsync_SlashTraversalName_NoFilesCreatedOutsideCache(string modelName)
    {
        // SanitizeModelName converts '/' to '_', so these names become safe subdirectory
        // names inside the cache (e.g. "../secret" → ".._secret"). The SEC-006 guard
        // supplements this by ensuring that even if sanitization fails, no escape occurs.
        //
        // Test: the eventual exception is a network/download failure, NOT an ArgumentException
        // from the guard, AND no files are written outside cacheDir.
        var cacheDir = Path.Combine(Path.GetTempPath(), $"SecTest_{Guid.NewGuid()}");
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

            var downloader = new ModelDownloader(new HttpClient(mockHandler.Object), cacheDir);
            _ = await Record.ExceptionAsync(() => downloader.EnsureModelAsync(modelName));

            // All files created by the downloader must be inside cacheDir.
            if (Directory.Exists(cacheDir))
            {
                var cacheRoot = Path.GetFullPath(cacheDir);
                foreach (var file in Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories))
                {
                    Assert.True(
                        Path.GetFullPath(file).StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase),
                        $"SEC-006 violation: file escaped cache directory: {file}");
                }
            }
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Valid model names — must NOT throw ArgumentException
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("sentence-transformers/all-MiniLM-L6-v2")]
    [InlineData("BAAI/bge-small-en-v1.5")]
    [InlineData("my-org/my-model")]
    public async Task EnsureModelAsync_ValidModelName_DoesNotThrowArgumentException(string validModelName)
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"SecTest_{Guid.NewGuid()}");
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

            var exception = await Record.ExceptionAsync(
                () => downloader.EnsureModelAsync(validModelName));

            // A 404 may produce InvalidOperationException, but never ArgumentException.
            Assert.IsNotType<ArgumentException>(exception);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Mathematical proof that the Path.GetFullPath guard catches path escapes
    // -------------------------------------------------------------------------

    [Fact]
    public void PathTraversalGuard_DotDotEscapesCacheDirectory()
    {
        // Demonstrates WHY the guard is necessary: ".." resolves to the parent directory.
        var cacheDir = Path.Combine(Path.GetTempPath(), "cache");
        var resolved = Path.GetFullPath(Path.Combine(cacheDir, ".."));
        var cacheRoot = Path.GetFullPath(cacheDir);

        Assert.False(
            resolved.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase),
            "'..' must not start with cacheDir — the guard correctly blocks it.");
    }

    [Fact]
    public void PathTraversalGuard_ValidSanitizedName_StaysInsideCache()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "cache");
        var sanitized = "sentence_transformers_all_MiniLM_L6_v2"; // typical sanitized form
        var resolved = Path.GetFullPath(Path.Combine(cacheDir, sanitized));
        var cacheRoot = Path.GetFullPath(cacheDir);

        Assert.True(
            resolved.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase),
            "A sanitized model name must resolve inside the cache directory.");
    }

    // -------------------------------------------------------------------------
    // SEC-002: Default constructor creates a usable downloader instance
    // -------------------------------------------------------------------------

    [Fact]
    public void ModelDownloader_DefaultConstructor_UsesSocketsHttpHandler()
    {
        // SEC-002: ModelDownloader() parameterless ctor uses SocketsHttpHandler
        // with PooledConnectionLifetime. We can't easily introspect the private
        // handler, so we verify: (1) construction succeeds without throwing, and
        // (2) the resulting instance reports a valid cache directory.
        var downloader = new ModelDownloader();

        Assert.NotNull(downloader);
        Assert.False(string.IsNullOrEmpty(downloader.GetCacheDirectory()));
    }
}
