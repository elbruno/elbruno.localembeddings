using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics.Tensors;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings;

/// <summary>
/// CLIP image encoder using ONNX Runtime and ImageSharp.
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
    public ClipImageEncoder(string modelPath)
    {
        _session = new InferenceSession(modelPath);
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
        using var image = Image.Load<Rgb24>(imagePath);
        return EncodeImage(image);
    }

    /// <summary>
    /// Encodes an image from a stream to an L2-normalized embedding vector.
    /// </summary>
    /// <param name="imageStream">A stream containing the image data.</param>
    /// <returns>An L2-normalized float array representing the image embedding.</returns>
    public float[] Encode(Stream imageStream)
    {
        using var image = Image.Load<Rgb24>(imageStream);
        return EncodeImage(image);
    }

    private float[] EncodeImage(Image<Rgb24> image)
    {
        image.Mutate(x => x.Resize(ImageSize, ImageSize));

        var tensor = new DenseTensor<float>(new[] { 1, 3, ImageSize, ImageSize });

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < ImageSize; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < ImageSize; x++)
                {
                    var pixel = pixelRow[x];

                    tensor[0, 0, y, x] = (pixel.R / 255.0f - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (pixel.G / 255.0f - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (pixel.B / 255.0f - Mean[2]) / Std[2];
                }
            }
        });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };

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
