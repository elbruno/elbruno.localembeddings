using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Runtime.InteropServices;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Manages an ONNX embedding model for inference.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="InferenceSession"/> used internally is thread-safe for concurrent 
/// <c>Run()</c> calls. Multiple threads can generate embeddings simultaneously without 
/// additional synchronization.
/// </para>
/// </remarks>
public sealed class OnnxEmbeddingModel : IDisposable
{
    private InferenceSession? _session;
    private string[]? _outputNames;
    private bool _disposed;
    private bool _normalizeEmbeddings;

    /// <summary>
    /// Gets the dimension of the embedding vectors produced by this model.
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
    /// <param name="normalizeEmbeddings">Whether to L2-normalize embeddings to unit length.</param>
    /// <param name="useParallelExecution">Whether to use parallel execution mode in ONNX Runtime.</param>
    /// <param name="interOpNumThreads">Optional inter-op thread count override.</param>
    /// <param name="intraOpNumThreads">Optional intra-op thread count override.</param>
    /// <exception cref="ArgumentException">Thrown when the model path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a model is already loaded.</exception>
    public void Load(
        string modelPath,
        bool normalizeEmbeddings = false,
        bool useParallelExecution = true,
        int? interOpNumThreads = null,
        int? intraOpNumThreads = null)
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

        SessionOptions sessionOptions;
        try
        {
            sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = useParallelExecution ? ExecutionMode.ORT_PARALLEL : ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = resolvedInterOpNumThreads,
                IntraOpNumThreads = resolvedIntraOpNumThreads
            };
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
        {
            throw new InvalidOperationException(
                BuildOnnxNativeLoadErrorMessage(modelPath),
                ex);
        }

        try
        {
            _session = new InferenceSession(modelPath, sessionOptions);
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
        {
            sessionOptions.Dispose();
            throw new InvalidOperationException(
                BuildOnnxNativeLoadErrorMessage(modelPath),
                ex);
        }

        _outputNames = _session.OutputMetadata.Keys.ToArray();
        _normalizeEmbeddings = normalizeEmbeddings;

        // Determine embedding dimension from model output
        var outputMeta = _session.OutputMetadata.Values.First();
        EmbeddingDimension = outputMeta.Dimensions.Length > 2 ? outputMeta.Dimensions[2] : outputMeta.Dimensions[^1];
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

    private static void ValidateThreadCount(int? threadCount, string paramName)
    {
        if (threadCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Thread count must be greater than zero when specified.");
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

    private static string BuildOnnxNativeLoadErrorMessage(string modelPath)
    {
        var osDescription = RuntimeInformation.OSDescription;
        var architecture = RuntimeInformation.ProcessArchitecture;
        var baseDirectory = AppContext.BaseDirectory;

        string? runtimeFolder = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "linux-arm64",
            Architecture.X64 => "linux-x64",
            Architecture.Arm => "linux-arm",
            _ => null
        };

        var nativeDirectory = runtimeFolder is null
            ? "<unknown>"
            : Path.Combine(baseDirectory, "runtimes", runtimeFolder, "native");

        return $"Failed to initialize ONNX Runtime native libraries. OS: {osDescription}; Architecture: {architecture}; Model path: '{modelPath}'; Base directory: '{baseDirectory}'; Expected native directory: '{nativeDirectory}'. On Linux, ensure ONNX native libraries are present and loadable (libonnxruntime.so and provider dependencies).";
    }

    /// <summary>
    /// Generates embeddings for the given tokenized input.
    /// </summary>
    /// <param name="inputIds">The tokenized input IDs.</param>
    /// <param name="attentionMask">The attention mask.</param>
    /// <returns>The embedding vector.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no model is loaded.</exception>
    /// <exception cref="ArgumentNullException">Thrown when inputIds or attentionMask is null.</exception>
    /// <exception cref="ArgumentException">Thrown when inputIds and attentionMask have different lengths.</exception>
    /// <remarks>
    /// This method is thread-safe and can be called concurrently from multiple threads.
    /// </remarks>
    public float[] GenerateEmbedding(long[] inputIds, long[] attentionMask)
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

        var embeddings = GenerateEmbeddings([inputIds], [attentionMask]);
        return embeddings[0];
    }

    /// <summary>
    /// Generates embeddings for multiple tokenized inputs in a single batched inference call.
    /// </summary>
    /// <param name="inputIds">Array of tokenized input ID sequences.</param>
    /// <param name="attentionMasks">Array of attention masks.</param>
    /// <returns>Array of embedding vectors, one per input.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no model is loaded.</exception>
    /// <exception cref="ArgumentNullException">Thrown when inputIds or attentionMasks is null.</exception>
    /// <exception cref="ArgumentException">Thrown when arrays have mismatched lengths or sequences have different lengths.</exception>
    /// <remarks>
    /// <para>
    /// Batched inference is more efficient than calling <see cref="GenerateEmbedding"/> multiple times,
    /// as it reduces overhead and enables better parallelization on the hardware.
    /// </para>
    /// <para>
    /// All sequences in the batch must have the same length. If your input sequences have different lengths,
    /// pad them to the same length with pad tokens (typically 0) and set the corresponding attention mask values to 0.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called concurrently from multiple threads.
    /// </para>
    /// </remarks>
    public float[][] GenerateEmbeddings(long[][] inputIds, long[][] attentionMasks)
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

        var batchSize = inputIds.Length;
        var sequenceLength = inputIds[0].Length;

        // Validate all sequences have the same length
        for (int i = 0; i < batchSize; i++)
        {
            if (inputIds[i].Length != sequenceLength)
            {
                throw new ArgumentException($"All input sequences must have the same length. Expected {sequenceLength}, got {inputIds[i].Length} at index {i}.");
            }

            if (attentionMasks[i].Length != sequenceLength)
            {
                throw new ArgumentException($"All attention masks must have the same length as input sequences. Expected {sequenceLength}, got {attentionMasks[i].Length} at index {i}.");
            }
        }

        // Flatten arrays for tensor creation
        var flatInputIds = new long[batchSize * sequenceLength];
        var flatAttentionMask = new long[batchSize * sequenceLength];
        var flatTokenTypeIds = new long[batchSize * sequenceLength]; // All zeros

        for (int i = 0; i < batchSize; i++)
        {
            Array.Copy(inputIds[i], 0, flatInputIds, i * sequenceLength, sequenceLength);
            Array.Copy(attentionMasks[i], 0, flatAttentionMask, i * sequenceLength, sequenceLength);
        }

        // Create tensors
        var shape = new long[] { batchSize, sequenceLength };

        var inputIdsTensor = new DenseTensor<long>(flatInputIds, [batchSize, sequenceLength]);
        var attentionMaskTensor = new DenseTensor<long>(flatAttentionMask, [batchSize, sequenceLength]);
        var tokenTypeIdsTensor = new DenseTensor<long>(flatTokenTypeIds, [batchSize, sequenceLength]);

        // Create input container
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        // Add token_type_ids if the model expects it
        if (_session.InputMetadata.ContainsKey("token_type_ids"))
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor));
        }

        // Run inference
        using var results = _session.Run(inputs, _outputNames);

        // Get output tensor - typically "last_hidden_state" with shape [batch, seq, hidden]
        var outputTensor = results.First().AsTensor<float>();

        // Apply mean pooling over the sequence dimension
        var embeddings = ApplyMeanPooling(outputTensor, attentionMasks, batchSize, sequenceLength);

        // Apply L2 normalization if enabled
        if (_normalizeEmbeddings)
        {
            for (int i = 0; i < embeddings.Length; i++)
            {
                L2Normalize(embeddings[i]);
            }
        }

        return embeddings;
    }

    /// <summary>
    /// Applies L2 normalization to a vector in-place, producing a unit-length vector.
    /// </summary>
    /// <param name="vector">The vector to normalize.</param>
    private static void L2Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }
    }

    /// <summary>
    /// Applies mean pooling over the sequence dimension, weighted by attention mask.
    /// </summary>
    private static float[][] ApplyMeanPooling(Tensor<float> outputTensor, long[][] attentionMasks, int batchSize, int sequenceLength)
    {
        var dimensions = outputTensor.Dimensions.ToArray();
        var hiddenSize = dimensions[^1];

        var embeddings = new float[batchSize][];

        for (int batch = 0; batch < batchSize; batch++)
        {
            var embedding = new float[hiddenSize];
            var tokenCount = 0L;

            // Sum over the sequence dimension, weighted by attention mask
            for (int seq = 0; seq < sequenceLength; seq++)
            {
                var mask = attentionMasks[batch][seq];
                if (mask == 0) continue;

                tokenCount += mask;
                for (int hidden = 0; hidden < hiddenSize; hidden++)
                {
                    embedding[hidden] += outputTensor[batch, seq, hidden] * mask;
                }
            }

            // Normalize by the number of tokens
            if (tokenCount > 0)
            {
                for (int hidden = 0; hidden < hiddenSize; hidden++)
                {
                    embedding[hidden] /= tokenCount;
                }
            }

            embeddings[batch] = embedding;
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
