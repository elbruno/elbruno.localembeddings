using System.Diagnostics;
using System.Security.Cryptography;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Serializes the downloads of a model directory across the processes that use the same cache, where the file system supports file locks.
/// </summary>
/// <remarks>
/// Every model downloader compiles this file, because not all of them reference ElBruno.LocalEmbeddings. On Unix, .NET opens the lock
/// file without a lock when file locking is disabled (the DOTNET_SYSTEM_IO_DISABLEFILELOCKING environment variable or the
/// System.IO.DisableFileLocking switch), or when flock fails with an error other than EWOULDBLOCK.
/// </remarks>
internal static class ModelDownloadLock
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    // A process that holds the lock for longer, for example one that hangs or an NFS client that is gone, makes the others fail with a
    // TimeoutException that names the lock file, instead of waiting without a limit.
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Opens <c>.download.lock</c> in the model directory with <see cref="FileShare.None"/>, waiting while another process holds it,
    /// for at most 30 minutes: then it throws a <see cref="TimeoutException"/> that names the lock file. The OS releases the lock when
    /// the process exits, so a crash of the process leaves no stale lock. On NFS, a client machine that crashes or loses the network
    /// keeps it until the server releases it: on NFS v4.1 when the lease of the client expires, on NFS v3 when the client starts again
    /// and notifies the server.
    /// It is opened for writing when possible, because on NFS v4.1 an exclusive flock on a descriptor open only for reading fails, and
    /// .NET ignores the failure. It is opened for reading only when it cannot be opened for writing, for example when another user created
    /// it in a shared cache or the cache is on a read-only file system. On NFS v4.1 a lock opened for reading does not exclude other clients.
    /// It returns null, and the download goes on without the lock, when the lock file cannot be created in an existing model directory,
    /// for example in a read-only cache, or exists and cannot be opened, for example when another user created it with mode 0600.
    /// </summary>
    internal static Task<FileStream?> AcquireAsync(string modelDirectory, CancellationToken cancellationToken)
        => AcquireAsync(modelDirectory, WaitTimeout, cancellationToken);

    // The tests pass a shorter time limit.
    internal static async Task<FileStream?> AcquireAsync(string modelDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(modelDirectory);
        var lockPath = Path.Combine(modelDirectory, ".download.lock");
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || (ex is IOException ioException && !IsHeldByAnotherProcess(ioException)))
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);
                }
            }
            catch (IOException ex) when (IsHeldByAnotherProcess(ex))
            {
                // Another process holds the lock while it downloads the same model.
                if (waited.Elapsed >= timeout)
                {
                    throw new TimeoutException(
                        $"Timed out after {timeout} waiting for the lock file '{lockPath}', which another process holds.", ex);
                }

                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (CannotWrite(ex) && !Directory.Exists(lockPath))
            {
                // The lock file cannot be created or opened: the download goes on without it, so that what it writes fails, if it does,
                // with its own error. A directory where the lock file should be is still an error.
                return null;
            }
        }
    }

    /// <summary>
    /// Runs a check that only reads the cache. An I/O error, for example a file that another process deletes meanwhile, counts as false.
    /// </summary>
    internal static bool IsComplete(Func<bool> check)
    {
        try
        {
            return check();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Reads only. True when the tokenizer files are there, no ONNX file is left in onnx/ to move, model.onnx and each quantized model file
    // that is there have a sidecar hash that matches them, and the expected hash, when given, matches the same file that the downloaders
    // verify after a download. Anything else, or an I/O error, takes the locked path, which checks and writes as before.
    internal static bool IsCompleteAndVerified(string modelDirectory, IReadOnlyList<string> tokenizerFiles, IReadOnlyList<string> quantizedModelFiles, bool preferQuantized, string? expectedHash)
    {
        try
        {
            if (tokenizerFiles.Any(f => !File.Exists(Path.Combine(modelDirectory, f))))
            {
                return false;
            }

            var onnxSubDir = Path.Combine(modelDirectory, "onnx");
            if (Directory.Exists(onnxSubDir)
                && Directory.GetFiles(onnxSubDir, "*.onnx").Any(f => !File.Exists(Path.Combine(modelDirectory, Path.GetFileName(f)))))
            {
                return false;
            }

            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in quantizedModelFiles.Prepend("model.onnx"))
            {
                var path = Path.Combine(modelDirectory, name);
                if (!File.Exists(path))
                {
                    continue;
                }

                var sidecarPath = path + ".sha256";
                if (!File.Exists(sidecarPath))
                {
                    return false;
                }

                var actual = ComputeSha256(path);
                if (!string.Equals(ReadAllText(sidecarPath).Trim(), actual, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                hashes[path] = actual;
            }

            if (hashes.Count == 0)
            {
                return false;
            }

            if (expectedHash != null)
            {
                var primaryModelPath = preferQuantized
                    ? quantizedModelFiles.Select(f => Path.Combine(modelDirectory, f)).FirstOrDefault(File.Exists)
                    : null;
                primaryModelPath ??= Path.Combine(modelDirectory, "model.onnx");

                if (!hashes.TryGetValue(primaryModelPath, out var primaryHash)
                    || !string.Equals(primaryHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Opens a file for reading with <see cref="FileShare.ReadWrite"/> and <see cref="FileShare.Delete"/>, so that a process on the
    /// locked path can still rewrite a sidecar hash or delete an invalid model while it is read.
    /// </summary>
    internal static FileStream OpenRead(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>
    /// Reads a text file the same way as <see cref="OpenRead"/>.
    /// </summary>
    internal static string ReadAllText(string path)
    {
        using var reader = new StreamReader(OpenRead(path));
        return reader.ReadToEnd();
    }

    private static string ComputeSha256(string path)
    {
        using var stream = OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    // Access denied, a read-only file system (EROFS on Unix, whose HResult is the errno: 30 on Linux and macOS) or a write-protected
    // medium (ERROR_WRITE_PROTECT on Windows).
    private static bool CannotWrite(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return true;
        }

        if (exception is not IOException)
        {
            return false;
        }

        return OperatingSystem.IsWindows() ? (exception.HResult & 0xFFFF) == 19 : exception.HResult == 30;
    }

    // The file is open with FileShare.None elsewhere: ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION on Windows, EWOULDBLOCK from flock
    // on Unix, whose HResult is the errno (11 on Linux, 35 on macOS). Any other I/O error is not retried.
    private static bool IsHeldByAnotherProcess(IOException exception)
    {
        if (OperatingSystem.IsWindows())
        {
            return (exception.HResult & 0xFFFF) is 32 or 33;
        }

        return exception.HResult == (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid() ? 11 : 35);
    }
}
