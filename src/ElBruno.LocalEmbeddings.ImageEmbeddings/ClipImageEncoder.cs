using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System.Numerics.Tensors;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings;

/// <summary>
/// CLIP image encoder using ONNX Runtime and SkiaSharp.
/// </summary>
/// <remarks>
/// Loads a CLIP vision encoder ONNX model and encodes images into
/// L2-normalized embedding vectors suitable for cross-modal similarity search.
/// Images are preprocessed to 224×224 pixels with CLIP normalization.
/// </remarks>
public sealed class ClipImageEncoder : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;

    // CLIP normalization parameters
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] Std = [0.26862954f, 0.26130258f, 0.27577711f];
    private const int ImageSize = 224;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipImageEncoder"/> class.
    /// </summary>
    /// <param name="modelPath">Path to the CLIP vision encoder ONNX model file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelPath"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
    public ClipImageEncoder(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath, nameof(modelPath));
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"CLIP vision model file not found: {modelPath}", modelPath);

        using var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Environment.ProcessorCount
        };
        _session = new InferenceSession(modelPath, sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();
        _outputName = _session.OutputMetadata.Keys.First();
    }

    /// <summary>
    /// Encodes an image file to an L2-normalized embedding vector.
    /// </summary>
    /// <param name="imagePath">Path to the image file to encode.</param>
    /// <returns>An L2-normalized float array representing the image embedding.</returns>
    public float[] Encode(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image file not found: {imagePath}", imagePath);

        using var image = SKBitmap.Decode(imagePath)
            ?? throw new InvalidDataException($"Unable to decode image: {imagePath}");
        return EncodeImage(image);
    }

    /// <summary>
    /// Encodes an image from a stream to an L2-normalized embedding vector.
    /// </summary>
    /// <param name="imageStream">A stream containing the image data.</param>
    /// <returns>An L2-normalized float array representing the image embedding.</returns>
    public float[] Encode(Stream imageStream)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        using var image = SKBitmap.Decode(imageStream)
            ?? throw new InvalidDataException("Unable to decode image stream.");
        return EncodeImage(image);
    }

    private float[] EncodeImage(SKBitmap image)
    {
        var tensor = CreateImageTensor(image);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>().ToArray();

        Normalize(output);

        return output;
    }

    internal static DenseTensor<float> CreateImageTensor(SKBitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var resizedImage = image.Resize(
            new SKImageInfo(ImageSize, ImageSize),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
            ?? throw new InvalidOperationException("Unable to resize image for CLIP preprocessing.");

        var pixels = resizedImage.Pixels;
        var tensor = new DenseTensor<float>(new[] { 1, 3, ImageSize, ImageSize });
        for (int y = 0; y < ImageSize; y++)
        {
            int rowOffset = y * ImageSize;
            for (int x = 0; x < ImageSize; x++)
            {
                var pixel = pixels[rowOffset + x];

                tensor[0, 0, y, x] = (pixel.Red / 255.0f - Mean[0]) / Std[0];
                tensor[0, 1, y, x] = (pixel.Green / 255.0f - Mean[1]) / Std[1];
                tensor[0, 2, y, x] = (pixel.Blue / 255.0f - Mean[2]) / Std[2];
            }
        }

        return tensor;
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
