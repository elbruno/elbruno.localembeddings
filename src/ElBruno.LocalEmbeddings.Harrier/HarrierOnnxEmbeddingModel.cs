using System.Buffers;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.LocalEmbeddings.Harrier;

/// <summary>
/// Manages an ONNX Harrier embedding model for inference.
/// </summary>
/// <remarks>
/// <para>
/// The Harrier ONNX model outputs <c>sentence_embedding</c> with shape <c>[batch, 640]</c>.
/// Pooling (last-token) and L2 normalization are baked into the ONNX graph, so this class
/// reads the output directly without additional post-processing.
/// </para>
/// <para>
/// The <see cref="InferenceSession"/> used internally is thread-safe for concurrent
/// <c>Run()</c> calls. Multiple threads can generate embeddings simultaneously without
/// additional synchronization.
/// </para>
/// </remarks>
public sealed class HarrierOnnxEmbeddingModel : IDisposable
{
    private InferenceSession? _session;
    private string[]? _outputNames;
    private bool _disposed;

    /// <summary>
    /// Gets the dimension of the embedding vectors produced by this model (640 for Harrier 270M).
    /// </summary>
    public int EmbeddingDimension { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the model is loaded and ready for inference.
    /// </summary>
    public bool IsLoaded => _session is not null;

    /// <summary>
    /// Loads the model from the specified path.
    /// </summary>
    /// <param name="modelPath">The path to the ONNX model file.</param>
    /// <param name="useParallelExecution">Whether to use parallel execution mode in ONNX Runtime.</param>
    /// <param name="interOpNumThreads">Optional inter-op thread count override.</param>
    /// <param name="intraOpNumThreads">Optional intra-op thread count override.</param>
    /// <param name="useDirectML">Whether to enable DirectML GPU acceleration (Windows only).</param>
    /// <param name="directMLDeviceId">The DirectML device ID to use when <paramref name="useDirectML"/> is true.</param>
    /// <exception cref="ArgumentException">Thrown when the model path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a model is already loaded.</exception>
    public void Load(
        string modelPath,
        bool useParallelExecution = true,
        int? interOpNumThreads = null,
        int? intraOpNumThreads = null,
        bool useDirectML = false,
        int directMLDeviceId = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new ArgumentException("Model path cannot be null or empty.", nameof(modelPath));
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("ONNX model file not found.", modelPath);
        }

        if (_session is not null)
        {
            throw new InvalidOperationException("A model is already loaded. Dispose this instance and create a new one to load a different model.");
        }

        ValidateThreadCount(interOpNumThreads, nameof(interOpNumThreads));
        ValidateThreadCount(intraOpNumThreads, nameof(intraOpNumThreads));

        EnsureLinuxOnnxRuntimeAliases();

        var defaultThreadCount = Environment.ProcessorCount;
        var resolvedInterOpNumThreads = interOpNumThreads ?? defaultThreadCount;
        var resolvedIntraOpNumThreads = intraOpNumThreads ?? defaultThreadCount;

        try
        {
            using var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = useParallelExecution ? ExecutionMode.ORT_PARALLEL : ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = resolvedInterOpNumThreads,
                IntraOpNumThreads = resolvedIntraOpNumThreads
            };
#if DIRECTML
            if (useDirectML)
            {
                sessionOptions.AppendExecutionProvider_DML(directMLDeviceId);
            }
#endif
            _session = new InferenceSession(modelPath, sessionOptions);
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
        {
            throw new InvalidOperationException(
                $"Failed to load ONNX Runtime native library. OS: {RuntimeInformation.OSDescription}, " +
                $"Arch: {RuntimeInformation.ProcessArchitecture}, Model: {modelPath}",
                ex);
        }

        _outputNames = _session.OutputMetadata.Keys.ToArray();

        // Determine embedding dimension from the sentence_embedding output [batch, dim]
        var outputMeta = _session.OutputMetadata.Values.First();
        EmbeddingDimension = outputMeta.Dimensions[^1];
    }

    private static void ValidateThreadCount(int? threadCount, string paramName)
    {
        if (threadCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Thread count must be greater than zero when specified.");
        }
    }

    private static void EnsureLinuxOnnxRuntimeAliases()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        string? runtimeFolder = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "linux-arm64",
            Architecture.X64 => "linux-x64",
            Architecture.Arm => "linux-arm",
            _ => null
        };

        if (runtimeFolder is null)
        {
            return;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var nativeDirectory = Path.Combine(baseDirectory, "runtimes", runtimeFolder, "native");
        var canonicalLibraryPath = Path.Combine(nativeDirectory, "libonnxruntime.so");

        if (!File.Exists(canonicalLibraryPath))
        {
            return;
        }

        var aliasNames = new[] { "onnxruntime.dll.so", "libonnxruntime.dll.so" };
        foreach (var aliasName in aliasNames)
        {
            TryCreateAliasCopy(canonicalLibraryPath, Path.Combine(nativeDirectory, aliasName));
            TryCreateAliasCopy(canonicalLibraryPath, Path.Combine(baseDirectory, aliasName));
        }
    }

    private static void TryCreateAliasCopy(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            return;
        }

        try
        {
            File.Copy(sourcePath, destinationPath);
        }
        catch
        {
            // Best effort only. If this fails, ONNX Runtime will still throw and
            // callers receive a detailed error message with platform diagnostics.
        }
    }

    /// <summary>
    /// Generates an embedding for the given tokenized input.
    /// </summary>
    public float[] GenerateEmbedding(long[] inputIds, long[] attentionMask)
        => GenerateEmbedding(inputIds, attentionMask, CancellationToken.None);

    /// <summary>
    /// Generates an embedding for the given tokenized input.
    /// </summary>
    public float[] GenerateEmbedding(long[] inputIds, long[] attentionMask, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(inputIds);
        ArgumentNullException.ThrowIfNull(attentionMask);

        if (inputIds.Length != attentionMask.Length)
        {
            throw new ArgumentException("inputIds and attentionMask must have the same length.");
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No model is loaded. Call Load() first.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var embeddings = GenerateEmbeddings([inputIds], [attentionMask], cancellationToken);
        return embeddings[0];
    }

    /// <summary>
    /// Generates embeddings for multiple tokenized inputs in a single batched inference call.
    /// </summary>
    /// <remarks>
    /// The Harrier ONNX model outputs <c>sentence_embedding [batch, dim]</c> directly —
    /// pooling and normalization are baked into the graph.
    /// </remarks>
    public float[][] GenerateEmbeddings(long[][] inputIds, long[][] attentionMasks)
        => GenerateEmbeddings(inputIds, attentionMasks, CancellationToken.None);

    /// <summary>
    /// Generates embeddings for multiple tokenized inputs in a single batched inference call.
    /// </summary>
    public float[][] GenerateEmbeddings(long[][] inputIds, long[][] attentionMasks, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(inputIds);
        ArgumentNullException.ThrowIfNull(attentionMasks);

        if (inputIds.Length == 0)
        {
            return [];
        }

        if (inputIds.Length != attentionMasks.Length)
        {
            throw new ArgumentException("inputIds and attentionMasks must have the same number of sequences.");
        }

        if (_session is null)
        {
            throw new InvalidOperationException("No model is loaded. Call Load() first.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var batchSize = inputIds.Length;
        var sequenceLength = inputIds[0].Length;

        for (int i = 0; i < batchSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inputIds[i].Length != sequenceLength)
            {
                throw new ArgumentException($"All input sequences must have the same length. Expected {sequenceLength}, got {inputIds[i].Length} at index {i}.");
            }

            if (attentionMasks[i].Length != sequenceLength)
            {
                throw new ArgumentException($"All attention masks must have the same length as input sequences. Expected {sequenceLength}, got {attentionMasks[i].Length} at index {i}.");
            }
        }

        int totalSize = batchSize * sequenceLength;
        var flatInputIds = ArrayPool<long>.Shared.Rent(totalSize);
        var flatAttentionMask = ArrayPool<long>.Shared.Rent(totalSize);
        try
        {
            for (int i = 0; i < batchSize; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Array.Copy(inputIds[i], 0, flatInputIds, i * sequenceLength, sequenceLength);
                Array.Copy(attentionMasks[i], 0, flatAttentionMask, i * sequenceLength, sequenceLength);
            }

            var inputIdsTensor = new DenseTensor<long>(flatInputIds.AsMemory(0, totalSize), [batchSize, sequenceLength]);
            var attentionMaskTensor = new DenseTensor<long>(flatAttentionMask.AsMemory(0, totalSize), [batchSize, sequenceLength]);

            // Harrier uses only input_ids and attention_mask (no token_type_ids)
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            using var results = _session.Run(inputs, _outputNames);
            cancellationToken.ThrowIfCancellationRequested();

            // Output is sentence_embedding [batch, dim] — already pooled and normalized
            var outputTensor = results.First().AsTensor<float>();
            return ExtractEmbeddings(outputTensor, batchSize);
        }
        finally
        {
            ArrayPool<long>.Shared.Return(flatInputIds);
            ArrayPool<long>.Shared.Return(flatAttentionMask);
        }
    }

    /// <summary>
    /// Extracts individual embedding vectors from the 2D output tensor [batch, dim].
    /// </summary>
    internal static float[][] ExtractEmbeddings(Tensor<float> outputTensor, int batchSize)
    {
        var embeddingDim = outputTensor.Dimensions[^1];
        var embeddings = new float[batchSize][];

        var denseTensor = (DenseTensor<float>)outputTensor;
        var tensorSpan = denseTensor.Buffer.Span;

        for (int batch = 0; batch < batchSize; batch++)
        {
            int offset = batch * embeddingDim;
            embeddings[batch] = tensorSpan.Slice(offset, embeddingDim).ToArray();
        }

        return embeddings;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _session?.Dispose();
        _session = null;
        _disposed = true;
    }
}
