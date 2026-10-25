using SkiaSharp;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

public class ClipImageEncoderPreprocessingTests
{
    [Fact]
    public void CreateImageTensor_ResizesAndNormalizesRgbChannels()
    {
        using var image = new SKBitmap(1, 1);
        image.SetPixel(0, 0, SKColors.Red);

        var tensor = ClipImageEncoder.CreateImageTensor(image);

        Assert.Equal(1, tensor.Dimensions[0]);
        Assert.Equal(3, tensor.Dimensions[1]);
        Assert.Equal(224, tensor.Dimensions[2]);
        Assert.Equal(224, tensor.Dimensions[3]);
        Assert.Equal((1f - 0.48145466f) / 0.26862954f, tensor[0, 0, 0, 0], 5);
        Assert.Equal((0f - 0.4578275f) / 0.26130258f, tensor[0, 1, 0, 0], 5);
        Assert.Equal((0f - 0.40821073f) / 0.27577711f, tensor[0, 2, 0, 0], 5);
    }

    [Fact]
    public void CreateImageTensor_NullImage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ClipImageEncoder.CreateImageTensor(null!));
    }
}
