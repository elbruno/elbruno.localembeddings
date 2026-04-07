using System.Buffers;
using System.Diagnostics;
using TensorPrimitives = System.Numerics.Tensors.TensorPrimitives;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.LocalEmbeddings.Npu;

/// <summary>
/// Manages an ONNX embedding model for NPU-accelerated inference via DirectML.
/// </summary>
/// <remarks>
/// <para>
/// This class uses the DirectML execution provider to leverage NPU hardware
/// (Intel Core Ultra, Qualcomm Snapdragon X, etc.) or GPU for accelerated inference.
/// </para>
/// <para>
/// The <see cref="InferenceSession"/> used internally is thread-safe for concurrent
/// <c>Run()</c> calls. Multiple threads can generate embeddings simultaneously without
/// additional synchronization.
/// </para>
/// </remarks>
public sealed class NpuOnnxEmbeddingModel : IDisposable
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
    /// Gets a value indicating whether the model is running on an NPU device.
    /// </summary>
    public bool IsNpuActive { get; private set; }

    /// <summary>
    /// Gets the reason why NPU hardware was not selected,
    /// or <c>null</c> if an NPU was found. Useful for diagnosing setup issues.
    /// </summary>
    public string? FallbackReason { get; private set; }

    /// <summary>
    /// Gets the description of the selected DirectML device.
    /// </summary>
    public string? DeviceDescription { get; private set; }

    /// <summary>
    /// Gets the DirectML device ID actually used for inference.
    /// </summary>
    public int ActiveDeviceId { get; private set; }

    /// <summary>
    /// Loads the model from the specified path with DirectML NPU acceleration.
    /// </summary>
    /// <param name="modelPath">The path to the ONNX model file.</param>
    /// <param name="normalizeEmbeddings">Whether to L2-normalize embeddings to unit length.</param>
    /// <param name="deviceId">The DirectML device ID (0 = default device). Overridden when <paramref name="autoDetectNpu"/> finds an NPU.</param>
    /// <param name="autoDetectNpu">When true, enumerates DXGI adapters to find and target NPU hardware instead of using <paramref name="deviceId"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the model path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a model is already loaded.</exception>
    public void Load(
        string modelPath,
        bool normalizeEmbeddings = false,
        int deviceId = 0,
        bool autoDetectNpu = false)
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

        if (deviceId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceId), "Device ID must be non-negative.");
        }

        int effectiveDeviceId = deviceId;

        if (autoDetectNpu && OperatingSystem.IsWindows())
        {
            try
            {
                var adapters = DxgiDeviceHelper.EnumerateAdapters();
                var npuAdapter = adapters.FirstOrDefault(a => a.IsLikelyNpu);

                if (npuAdapter is not null)
                {
                    effectiveDeviceId = npuAdapter.Index;
                    DeviceDescription = npuAdapter.Description;
                    IsNpuActive = true;
                }
                else
                {
                    var selectedAdapter = adapters.FirstOrDefault(a => a.Index == deviceId);
                    DeviceDescription = selectedAdapter?.Description;
                    var adapterNames = string.Join(", ", adapters.Select(a => $"[{a.Index}] {a.Description}"));
                    FallbackReason = adapters.Count == 0
                        ? "No DXGI adapters found. Ensure GPU/NPU drivers are installed."
                        : $"No NPU adapter detected. Available: {adapterNames}";
                    IsNpuActive = false;

                    // Record fallback telemetry
                    NpuDiagnostics.RecordFallback(null, "DirectML-NPU", "DirectML-GPU", FallbackReason);
                }
            }
            catch (Exception)
            {
                FallbackReason = "DXGI adapter enumeration failed. Using specified device ID.";
                IsNpuActive = false;
                NpuDiagnostics.RecordFallback(null, "DirectML-NPU", "DirectML", FallbackReason);
            }
        }

        try
        {
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            sessionOptions.AppendExecutionProvider_DML(effectiveDeviceId);
            _session = new InferenceSession(modelPath, sessionOptions);
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
        {
            throw new InvalidOperationException(
                $"Failed to initialize ONNX Runtime with DirectML execution provider. " +
                $"Ensure Microsoft.ML.OnnxRuntime.DirectML native binaries are available. " +
                $"Model path: '{modelPath}'; Device ID: {effectiveDeviceId}.",
                ex);
        }

        ActiveDeviceId = effectiveDeviceId;
        _outputNames = _session.OutputMetadata.Keys.ToArray();
        _normalizeEmbeddings = normalizeEmbeddings;

        var outputMeta = _session.OutputMetadata.Values.First();
        EmbeddingDimension = outputMeta.Dimensions.Length > 2 ? outputMeta.Dimensions[2] : outputMeta.Dimensions[^1];
    }

    /// <summary>
    /// Generates embeddings for the given tokenized input.
    /// </summary>
    /// <param name="inputIds">The tokenized input IDs.</param>
    /// <param name="attentionMask">The attention mask.</param>
    /// <returns>The embedding vector.</returns>
    public float[] GenerateEmbedding(long[] inputIds, long[] attentionMask)
        => GenerateEmbedding(inputIds, attentionMask, CancellationToken.None);

    /// <summary>
    /// Generates an embedding for the given tokenized input.
    /// </summary>
    /// <param name="inputIds">The tokenized input IDs.</param>
    /// <param name="attentionMask">The attention mask.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>The embedding vector.</returns>
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
    /// <param name="inputIds">Array of tokenized input ID sequences.</param>
    /// <param name="attentionMasks">Array of attention masks.</param>
    /// <returns>Array of embedding vectors, one per input.</returns>
    public float[][] GenerateEmbeddings(long[][] inputIds, long[][] attentionMasks)
        => GenerateEmbeddings(inputIds, attentionMasks, CancellationToken.None);

    /// <summary>
    /// Generates embeddings for multiple tokenized inputs in a single batched inference call.
    /// </summary>
    /// <param name="inputIds">Array of tokenized input ID sequences.</param>
    /// <param name="attentionMasks">Array of attention masks.</param>
    /// <param name="cancellationToken">Token used to cancel execution.</param>
    /// <returns>Array of embedding vectors, one per input.</returns>
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

        var executionProvider = IsNpuActive ? "DirectML-NPU" : "DirectML";
        using var activity = NpuDiagnostics.StartInference(executionProvider);

        // Record device selection info
        NpuDiagnostics.RecordDeviceSelection(activity, ActiveDeviceId, DeviceDescription, IsNpuActive);

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
        var flatTokenTypeIds = ArrayPool<long>.Shared.Rent(totalSize);
        try
        {
            flatTokenTypeIds.AsSpan(0, totalSize).Clear();

            for (int i = 0; i < batchSize; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Array.Copy(inputIds[i], 0, flatInputIds, i * sequenceLength, sequenceLength);
                Array.Copy(attentionMasks[i], 0, flatAttentionMask, i * sequenceLength, sequenceLength);
            }

            var inputIdsTensor = new DenseTensor<long>(flatInputIds.AsMemory(0, totalSize), [batchSize, sequenceLength]);
            var attentionMaskTensor = new DenseTensor<long>(flatAttentionMask.AsMemory(0, totalSize), [batchSize, sequenceLength]);
            var tokenTypeIdsTensor = new DenseTensor<long>(flatTokenTypeIds.AsMemory(0, totalSize), [batchSize, sequenceLength]);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            if (_session.InputMetadata.ContainsKey("token_type_ids"))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor));
            }

            using var results = _session.Run(inputs, _outputNames);
            cancellationToken.ThrowIfCancellationRequested();

            var outputTensor = results.First().AsTensor<float>();
            var embeddings = ApplyMeanPooling(outputTensor, attentionMasks, batchSize, sequenceLength);

            if (_normalizeEmbeddings)
            {
                for (int i = 0; i < embeddings.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    L2Normalize(embeddings[i]);
                }
            }

            return embeddings;
        }
        finally
        {
            ArrayPool<long>.Shared.Return(flatInputIds);
            ArrayPool<long>.Shared.Return(flatAttentionMask);
            ArrayPool<long>.Shared.Return(flatTokenTypeIds);
        }
    }

    private static void L2Normalize(float[] vector)
    {
        var norm = TensorPrimitives.Norm(vector);
        if (norm > 0)
        {
            TensorPrimitives.Divide(vector, norm, vector);
        }
    }

    internal static float[][] ApplyMeanPooling(Tensor<float> outputTensor, long[][] attentionMasks, int batchSize, int sequenceLength)
    {
        var dimensions = outputTensor.Dimensions.ToArray();
        var hiddenSize = dimensions[^1];

        var embeddings = new float[batchSize][];

        var denseTensor = (DenseTensor<float>)outputTensor;
        var tensorSpan = denseTensor.Buffer.Span;

        for (int batch = 0; batch < batchSize; batch++)
        {
            var embedding = new float[hiddenSize];
            int tokenCount = 0;
            var masks = attentionMasks[batch];

            for (int seq = 0; seq < sequenceLength; seq++)
            {
                if (masks[seq] == 0) continue;

                tokenCount++;
                int offset = (batch * sequenceLength + seq) * hiddenSize;
                TensorPrimitives.Add(embedding, tensorSpan.Slice(offset, hiddenSize), embedding);
            }

            if (tokenCount > 0)
            {
                TensorPrimitives.Divide(embedding, (float)tokenCount, embedding);
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
