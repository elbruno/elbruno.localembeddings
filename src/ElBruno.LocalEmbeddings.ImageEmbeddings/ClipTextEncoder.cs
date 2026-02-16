using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Numerics.Tensors;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings;

/// <summary>
/// CLIP text encoder using ONNX Runtime.
/// </summary>
/// <remarks>
/// Loads a CLIP text encoder ONNX model and encodes text strings into
/// L2-normalized embedding vectors suitable for cross-modal similarity search.
/// </remarks>
public sealed class ClipTextEncoder : IDisposable
{
    private readonly InferenceSession _session;
    private readonly ClipTokenizer _tokenizer;
    private readonly string _inputIdsName;
    private readonly string? _attentionMaskName;
    private readonly string _outputName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipTextEncoder"/> class.
    /// </summary>
    /// <param name="modelPath">Path to the CLIP text encoder ONNX model file.</param>
    /// <param name="vocabPath">Path to the vocabulary JSON file.</param>
    /// <param name="mergesPath">Path to the BPE merge rules file.</param>
    public ClipTextEncoder(string modelPath, string vocabPath, string mergesPath)
    {
        _session = new InferenceSession(modelPath);
        _tokenizer = new ClipTokenizer(vocabPath, mergesPath);

        var inputKeys = _session.InputMetadata.Keys.ToArray();
        if (inputKeys.Length == 0)
        {
            throw new InvalidOperationException("The CLIP text model does not define any inputs.");
        }

        _inputIdsName = inputKeys.FirstOrDefault(k => k.Contains("input_ids", StringComparison.OrdinalIgnoreCase)
            || k.Contains("input", StringComparison.OrdinalIgnoreCase)) ?? inputKeys[0];
        _attentionMaskName = inputKeys.FirstOrDefault(k => k.Contains("attention_mask", StringComparison.OrdinalIgnoreCase)
            || k.Contains("mask", StringComparison.OrdinalIgnoreCase));

        var outputKeys = _session.OutputMetadata.Keys.ToArray();
        if (outputKeys.Length == 0)
        {
            throw new InvalidOperationException("The CLIP text model does not define any outputs.");
        }

        _outputName = outputKeys[0];
    }

    /// <summary>
    /// Encodes text to an L2-normalized embedding vector.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>An L2-normalized float array representing the text embedding.</returns>
    public float[] Encode(string text)
    {
        var (inputIds, attentionMask) = _tokenizer.Encode(text);

        var inputIdsTensor = new DenseTensor<long>(new[] { 1, 77 });
        var attentionMaskTensor = new DenseTensor<long>(new[] { 1, 77 });

        for (int i = 0; i < 77; i++)
        {
            inputIdsTensor[0, i] = inputIds[i];
            attentionMaskTensor[0, i] = attentionMask[i];
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputIdsName, inputIdsTensor)
        };

        if (_attentionMaskName != null)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(_attentionMaskName, attentionMaskTensor));
        }

        using var results = _session.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();

        Normalize(output);

        return output;
    }

    private static void Normalize(float[] vector)
    {
        var span = vector.AsSpan();
        float norm = MathF.Sqrt(TensorPrimitives.SumOfSquares(span));
        if (norm > 0)
        {
            TensorPrimitives.Divide(span, norm, span);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session?.Dispose();
    }
}
