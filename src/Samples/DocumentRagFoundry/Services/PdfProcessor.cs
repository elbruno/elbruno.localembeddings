using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace DocumentRagFoundry.Services;

/// <summary>
/// Processes PDF documents to extract text and convert pages to images
/// </summary>
public class PdfProcessor
{
    /// <summary>
    /// Represents a text segment from a PDF page
    /// </summary>
    public record TextSegment(string Text, string SourceFile, int PageNumber);

    /// <summary>
    /// Represents an image converted from a PDF page
    /// </summary>
    public record PageImage(string ImagePath, string SourceFile, int PageNumber);

    /// <summary>
    /// Extracts text content from a PDF file
    /// </summary>
    public async Task<List<TextSegment>> ExtractTextAsync(string pdfPath)
    {
        var segments = new List<TextSegment>();
        var fileName = Path.GetFileName(pdfPath);

        await Task.Run(() =>
        {
            using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);

            for (int pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                var page = document.Pages[pageIndex];

                // Extract text content from page
                // Note: PdfSharp doesn't have built-in text extraction
                // For this sample, we'll use a simplified approach
                // In production, consider using a library like iTextSharp or PdfPig
                var content = ExtractTextFromPage(page);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    segments.Add(new TextSegment(
                        Text: content.Trim(),
                        SourceFile: fileName,
                        PageNumber: pageIndex + 1
                    ));
                }
            }
        });

        return segments;
    }

    /// <summary>
    /// Converts PDF pages to images
    /// </summary>
    public async Task<List<PageImage>> ConvertPagesToImagesAsync(string pdfPath, string outputDirectory)
    {
        var pageImages = new List<PageImage>();
        var fileName = Path.GetFileNameWithoutExtension(pdfPath);

        // Ensure output directory exists
        Directory.CreateDirectory(outputDirectory);

        await Task.Run(() =>
        {
            using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);

            for (int pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                var page = document.Pages[pageIndex];
                var imagePath = Path.Combine(
                    outputDirectory,
                    $"{fileName}_page_{pageIndex + 1}.png"
                );

                // Render PDF page to image
                // Note: This is a simplified implementation
                // For production, consider using a proper PDF rendering library
                RenderPageToImage(page, imagePath);

                pageImages.Add(new PageImage(
                    ImagePath: imagePath,
                    SourceFile: Path.GetFileName(pdfPath),
                    PageNumber: pageIndex + 1
                ));
            }
        });

        return pageImages;
    }

    private string ExtractTextFromPage(PdfPage page)
    {
        // PdfSharp doesn't natively support text extraction
        // This is a placeholder - in a real implementation, you would:
        // 1. Use a library like PdfPig or iTextSharp for text extraction
        // 2. Or use OCR if the PDF is image-based

        // For now, we'll extract from the page's content stream if available
        try
        {
            var content = page.Contents;
            if (content?.Elements?.Count > 0)
            {
                // Very basic extraction - just for demonstration
                // In production, use a proper PDF text extraction library
                return $"[Text from {page.Owner.FullPath} - Page {page.CustomValues}]";
            }
        }
        catch
        {
            // Ignore extraction errors for now
        }

        return string.Empty;
    }

    private void RenderPageToImage(PdfPage page, string outputPath)
    {
        // PdfSharp doesn't include rendering capabilities
        // This is a placeholder implementation
        // In production, you would use:
        // 1. PDFium.NET for high-quality rendering
        // 2. Or a combination of ghostscript/ImageMagick
        // 3. Or Windows.Data.Pdf on Windows

        // For this sample, create a placeholder image
        var info = new SKImageInfo(
            width: (int)page.Width.Point,
            height: (int)page.Height.Point,
            colorType: SKColorType.Rgba8888,
            alphaType: SKAlphaType.Premul
        );

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // Draw white background
        canvas.Clear(SKColors.White);

        // Add placeholder text
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        using var font = new SKFont
        {
            Size = 24
        };

        canvas.DrawText(
            $"PDF Page Placeholder\n{Path.GetFileName(page.Owner.FullPath)}",
            x: 50,
            y: 100,
            font,
            paint
        );

        // Save as PNG
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
    }
}
