using System.Buffers;
using TensorPrimitives = System.Numerics.Tensors.TensorPrimitives;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.LocalEmbeddings.Npu.Intel;

/// <summary>
/// Manages an ONNX embedding model for Intel Core Ultra NPU-accelerated inference via OpenVINO.
/// </summary>
/// <remarks>
/// <para>
/// This class uses the Intel OpenVINO execution provider to leverage the Intel AI Boost NPU
/// found in Intel Core Ultra processors for accelerated inference.
/// </para>
/// <para>
/// If the OpenVINO execution provider is not available (e.g., OpenVINO not installed or
/// running on non-Intel hardware), the model falls back to CPU execution automatically.
/// </para>
/// <para>
/// <strong>Prerequisites:</strong> Intel OpenVINO runtime must be installed.
/// Run <c>setupvars.bat</c> (Windows) before launching your application to set
/// the required environment variables.
/// </para>
/// </remarks>
public sealed class IntelOnnxEmbeddingModel : IDisposable
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
    /// Gets a value indicating whether the OpenVINO execution provider is active.
    /// When false, inference is running on CPU as a fallback.
    /// </summary>
    public bool IsOpenVinoActive { get; private set; }

    /// <summary>
    /// Gets the reason why the OpenVINO execution provider could not be loaded,
    /// or <c>null</c> if OpenVINO is active. Useful for diagnosing NPU setup issues.
    /// </summary>
    public string? FallbackReason { get; private set; }

    /// <summary>
    /// Loads the model from the specified path with Intel OpenVINO NPU acceleration.
    /// </summary>
    /// <param name="modelPath">The path to the ONNX model file.</param>
    /// <param name="normalizeEmbeddings">Whether to L2-normalize embeddings to unit length.</param>
    /// <param name="deviceType">The OpenVINO device type (default: "NPU").</param>
    /// <param name="fallbackToCpu">Whether to fall back to CPU if OpenVINO is unavailable.</param>
    public void Load(
        string modelPath,
        bool normalizeEmbeddings = false,
        string deviceType = "NPU",
        bool fallbackToCpu = true)
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

        _session = CreateSession(modelPath, deviceType, fallbackToCpu);
        _outputNames = _session.OutputMetadata.Keys.ToArray();
        _normalizeEmbeddings = normalizeEmbeddings;

        var outputMeta = _session.OutputMetadata.Values.First();
        EmbeddingDimension = outputMeta.Dimensions.Length > 2 ? outputMeta.Dimensions[2] : outputMeta.Dimensions[^1];
    }

    private InferenceSession CreateSession(string modelPath, string deviceType, bool fallbackToCpu)
    {
        // Try OpenVINO execution provider first
        try
        {
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            // Use the generic AppendExecutionProvider API for OpenVINO
            sessionOptions.AppendExecutionProvider_OpenVINO(deviceType);

            var session = new InferenceSession(modelPath, sessionOptions);
            IsOpenVinoActive = true;
            return session;
        }
        catch (EntryPointNotFoundException)
        {
            // OpenVINO entry point doesn't exist in this ORT version — likely a version conflict
            FallbackReason = "OpenVINO EP not found in loaded ORT runtime. " +
                "This typically happens when another package (e.g., Microsoft.ML.OnnxRuntime.DirectML) " +
                "loads ORT 1.24+ which doesn't include the OpenVINO EP entry point. " +
                "Run the Intel NPU project standalone (without DirectML) to avoid this conflict.";

            if (!fallbackToCpu)
                throw new InvalidOperationException(FallbackReason);
        }
        catch (Exception ex) when (fallbackToCpu)
        {
            // OpenVINO not available — fall back to CPU
            FallbackReason = ex.Message;
        }

        // CPU fallback
        try
        {
            var cpuOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_PARALLEL,
                InterOpNumThreads = Environment.ProcessorCount,
                IntraOpNumThreads = Environment.ProcessorCount
            };

            var session = new InferenceSession(modelPath, cpuOptions);
            IsOpenVinoActive = false;
            return session;
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
        {
            throw new InvalidOperationException(
                $"Failed to initialize ONNX Runtime. " +
                $"OpenVINO execution provider was not available, and CPU fallback also failed. " +
                $"Ensure ONNX Runtime native binaries are available. Model path: '{modelPath}'.",
                ex);
        }
    }

    /// <summary>
    /// Generates embeddings for the given tokenized input.
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
