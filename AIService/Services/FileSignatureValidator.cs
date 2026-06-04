using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace AIService.Services;

public sealed class FileSignatureValidator : IFileSignatureValidator
{
    public bool HasValidSignature(string filePath, string fileExtension)
    {
        string extension = NormalizeExtension(fileExtension);

        if (!File.Exists(filePath))
        {
            return false;
        }

        // Kiểm tra cả magic bytes và khả năng mở file để tránh giả mạo extension.
        return extension switch
        {
            ".pdf" => HasPdfSignature(filePath) && CanOpenPdf(filePath),
            ".docx" => HasZipSignature(filePath) && CanOpenDocx(filePath),
            ".pptx" => HasZipSignature(filePath) && CanOpenPptx(filePath),
            _ => false
        };
    }

    private static bool HasPdfSignature(string filePath)
    {
        byte[] header = ReadHeader(filePath, 5);
        return header.Length >= 5 && Encoding.ASCII.GetString(header) == "%PDF-";
    }

    private static bool HasZipSignature(string filePath)
    {
        byte[] header = ReadHeader(filePath, 4);

        if (header.Length < 4)
        {
            return false;
        }

        return header[0] == 0x50 &&
               header[1] == 0x4B &&
               (header[2], header[3]) is (0x03, 0x04) or (0x05, 0x06) or (0x07, 0x08);
    }

    private static bool CanOpenPdf(string filePath)
    {
        try
        {
            using PdfDocument document = PdfDocument.Open(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanOpenDocx(string filePath)
    {
        try
        {
            using WordprocessingDocument document = WordprocessingDocument.Open(filePath, false);
            return document.MainDocumentPart?.Document is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanOpenPptx(string filePath)
    {
        try
        {
            using PresentationDocument document = PresentationDocument.Open(filePath, false);
            return document.PresentationPart?.Presentation is not null;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ReadHeader(string filePath, int length)
    {
        using FileStream stream = File.OpenRead(filePath);
        byte[] buffer = new byte[length];
        int read = stream.Read(buffer, 0, buffer.Length);

        return read == buffer.Length ? buffer : buffer[..read];
    }

    private static string NormalizeExtension(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            return string.Empty;
        }

        return fileExtension.StartsWith('.')
            ? fileExtension.ToLowerInvariant()
            : $".{fileExtension.ToLowerInvariant()}";
    }
}
