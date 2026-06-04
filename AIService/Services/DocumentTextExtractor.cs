using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using DrawingParagraph = DocumentFormat.OpenXml.Drawing.Paragraph;
using DrawingText = DocumentFormat.OpenXml.Drawing.Text;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace AIService.Services;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx",
        ".pptx"
    };

    public bool CanExtract(string fileExtension)
    {
        return SupportedExtensions.Contains(NormalizeExtension(fileExtension));
    }

    public Task<string> ExtractTextAsync(
        string filePath,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        // Mỗi định dạng có parser riêng để tránh xử lý file nhị phân bằng string thô.
        string extension = NormalizeExtension(fileExtension);

        return extension switch
        {
            ".pdf" => Task.FromResult(ExtractPdfText(filePath)),
            ".docx" => Task.FromResult(ExtractDocxText(filePath)),
            ".pptx" => Task.FromResult(ExtractPptxText(filePath)),
            _ => Task.FromResult(string.Empty)
        };
    }

    private static string ExtractPdfText(string filePath)
    {
        var builder = new StringBuilder();

        using PdfDocument document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string ExtractDocxText(string filePath)
    {
        using WordprocessingDocument document = WordprocessingDocument.Open(filePath, false);

        IEnumerable<string> paragraphs = document.MainDocumentPart?
            .Document?
            .Body?
            .Descendants<WordParagraph>()
            .Select(paragraph => string.Concat(paragraph.Descendants<WordText>().Select(element => element.Text)).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text)) ?? Enumerable.Empty<string>();

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static string ExtractPptxText(string filePath)
    {
        using PresentationDocument document = PresentationDocument.Open(filePath, false);

        IEnumerable<string> text = document.PresentationPart?
            .SlideParts
            .SelectMany(slide => slide.Slide?.Descendants<DrawingParagraph>() ?? Enumerable.Empty<DrawingParagraph>())
            .Select(paragraph => string.Concat(paragraph.Descendants<DrawingText>().Select(element => element.Text)).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line)) ?? Enumerable.Empty<string>();

        return string.Join(Environment.NewLine, text);
    }

    private static string NormalizeExtension(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            return string.Empty;
        }

        return fileExtension.StartsWith('.')
            ? fileExtension
            : $".{fileExtension}";
    }
}
