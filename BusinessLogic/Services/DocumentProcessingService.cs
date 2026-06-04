using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AIService.Models;
using AIService.Services;
using BusinessObjects.Entities;
using BusinessObjects.Enums;

namespace BusinessLogic.Services;

public sealed class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly ITextChunkingService _textChunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public DocumentProcessingService(
        IDocumentTextExtractor textExtractor,
        ITextChunkingService textChunkingService,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore)
    {
        _textExtractor = textExtractor;
        _textChunkingService = textChunkingService;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    public async Task<DocumentTextProcessingResult> ExtractAndChunkAsync(
        string physicalPath,
        string fileExtension,
        string originalFileName,
        string currentTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_textExtractor.CanExtract(fileExtension))
            {
                return new DocumentTextProcessingResult(
                    currentTitle,
                    DocumentProcessingStatus.Uploaded.ToString(),
                    null,
                    Array.Empty<DocumentChunkProcessingResult>());
            }

            string text = await _textExtractor.ExtractTextAsync(
                physicalPath,
                fileExtension,
                cancellationToken);
            string title = ResolveDocumentTitle(currentTitle, originalFileName, text);
            IReadOnlyList<DocumentChunkProcessingResult> chunks = _textChunkingService
                .Chunk(text)
                .Select(chunk => new DocumentChunkProcessingResult(
                    chunk.Index,
                    chunk.Content,
                    chunk.EstimatedTokenCount,
                    chunk.SectionTitle,
                    $"vec_{Guid.NewGuid():N}",
                    DateTime.UtcNow))
                .ToList();

            // Extract/chunk chỉ chuẩn bị metadata; vector sẽ được lưu sau khi Document có id trong database.
            return new DocumentTextProcessingResult(
                title,
                DocumentProcessingStatus.Uploaded.ToString(),
                null,
                chunks);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"[Document Processing] Extract/chunk failed for '{originalFileName}'. {exception.Message}");
            System.Diagnostics.Debug.WriteLine(exception.ToString());

            return new DocumentTextProcessingResult(
                currentTitle,
                DocumentProcessingStatus.Failed.ToString(),
                exception.Message,
                Array.Empty<DocumentChunkProcessingResult>());
        }
    }

    public async Task<DocumentVectorProcessingResult> StoreVectorsIfReadyAsync(
        Document document,
        string storageRootPath,
        CancellationToken cancellationToken = default)
    {
        if (document.ProcessingStatus == DocumentProcessingStatus.Failed.ToString() ||
            document.DocumentChunks.Count == 0)
        {
            return new DocumentVectorProcessingResult(document.ProcessingStatus, null, null);
        }

        try
        {
            foreach (DocumentChunk chunk in document.DocumentChunks.OrderBy(chunk => chunk.ChunkIndex))
            {
                chunk.VectorId ??= $"vec_{Guid.NewGuid():N}";
                // Embedding dùng task RetrievalDocument để đồng bộ với query embedding khi chat.
                float[] embedding = await _embeddingService.GenerateEmbeddingAsync(
                    chunk.Content,
                    EmbeddingTaskType.RetrievalDocument,
                    cancellationToken);

                await _vectorStore.UpsertAsync(
                    new VectorRecord(
                        chunk.VectorId,
                        embedding,
                        document.DocumentId,
                        chunk.DocumentChunkId),
                    storageRootPath,
                    cancellationToken);
            }

            return new DocumentVectorProcessingResult(
                DocumentProcessingStatus.Indexed.ToString(),
                null,
                DateTime.UtcNow);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"[Document Processing] Vector indexing failed for DocumentId={document.DocumentId}, File='{document.OriginalFileName}'. {exception.Message}");
            System.Diagnostics.Debug.WriteLine(exception.ToString());

            return new DocumentVectorProcessingResult(
                DocumentProcessingStatus.Failed.ToString(),
                exception.Message,
                DateTime.UtcNow);
        }
    }

    public async Task DeleteVectorsIfExistsAsync(
        int documentId,
        string storageRootPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _vectorStore.DeleteByDocumentIdAsync(documentId, storageRootPath, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            // Metadata deletion must not be blocked by a stale local vector-store file.
        }
    }

    private static string ResolveDocumentTitle(
        string currentTitle,
        string originalFileName,
        string extractedText)
    {
        string? titleFromText = ExtractDocumentTitleFromText(extractedText);

        if (!string.IsNullOrWhiteSpace(titleFromText))
        {
            return titleFromText;
        }

        string? titleFromFileName = CleanDocumentTitle(Path.GetFileNameWithoutExtension(originalFileName));

        if (!string.IsNullOrWhiteSpace(titleFromFileName))
        {
            return titleFromFileName;
        }

        return string.IsNullOrWhiteSpace(currentTitle)
            ? "Tài liệu"
            : currentTitle.Trim();
    }

    private static string? ExtractDocumentTitleFromText(string extractedText)
    {
        string[] candidateLines = extractedText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0)
            .Take(15)
            .ToArray();

        foreach (string line in candidateLines)
        {
            if (IsGenericDocumentTitle(line))
            {
                continue;
            }

            if (LooksLikeDocumentTitle(line))
            {
                return CleanDocumentTitle(line);
            }
        }

        string? firstUsefulLine = candidateLines.FirstOrDefault(line => !IsGenericDocumentTitle(line));

        return firstUsefulLine is null ? null : CleanDocumentTitle(firstUsefulLine);
    }

    private static bool LooksLikeDocumentTitle(string line)
    {
        if (line.Length is < 4 or > 180)
        {
            return false;
        }

        if (Regex.IsMatch(line, @"^(?:chapter|chương|chuong)\s+\d+\s*[-–:]", RegexOptions.IgnoreCase))
        {
            return false;
        }

        int wordCount = Regex.Split(line, @"\s+").Count(word => !string.IsNullOrWhiteSpace(word));

        return wordCount is >= 2 and <= 24 &&
               (!line.EndsWith('.') || UppercaseLetterRatio(line) >= 0.45);
    }

    private static string? CleanDocumentTitle(string value)
    {
        string title = Regex
            .Replace(value.Replace('_', ' '), @"\s+", " ")
            .Trim(' ', '-', '–', ':', ';', '.', ',');

        if (title.Length == 0)
        {
            return null;
        }

        string[] dashParts = Regex
            .Split(title, @"\s+[-–]\s+")
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();

        if (dashParts.Length >= 2 && IsGenericTitlePrefix(dashParts[0]))
        {
            title = dashParts[^1];
        }

        title = Regex.Replace(
            title,
            @"^(?:tên\s+dự\s+án|ten\s+du\s+an|project|document|tài\s+liệu|tai\s+lieu)\s*[:\-–]\s*",
            string.Empty,
            RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"\s+", " ").Trim(' ', '-', '–', ':', ';', '.', ',');

        if (title.Length == 0 || IsGenericDocumentTitle(title))
        {
            return null;
        }

        return ToVietnameseTitleCase(title);
    }

    private static bool IsGenericDocumentTitle(string value)
    {
        string normalized = NormalizeForTitleComparison(value);

        string[] genericTitles =
        [
            "introduction",
            "gioi thieu",
            "muc luc",
            "noi dung",
            "table of contents",
            "document",
            "tai lieu"
        ];

        return genericTitles.Any(title => string.Equals(normalized, title, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGenericTitlePrefix(string value)
    {
        string normalized = NormalizeForTitleComparison(value);

        string[] genericPrefixes =
        [
            "cot truyen",
            "noi dung",
            "tai lieu",
            "workflow",
            "chapter",
            "day du",
            "full"
        ];

        return genericPrefixes.Any(prefix => normalized.Contains(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToVietnameseTitleCase(string value)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("vi-VN");
        string normalized = Regex.Replace(value, @"\s+", " ").Trim();
        bool mostlyUppercase = UppercaseLetterRatio(normalized) >= 0.55;

        if (!mostlyUppercase)
        {
            return normalized;
        }

        string lowered = culture.TextInfo.ToLower(normalized);

        return culture.TextInfo.ToTitleCase(lowered);
    }

    private static double UppercaseLetterRatio(string value)
    {
        int letterCount = 0;
        int uppercaseCount = 0;

        foreach (char character in value)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            letterCount++;

            if (char.IsUpper(character))
            {
                uppercaseCount++;
            }
        }

        return letterCount == 0 ? 0 : (double)uppercaseCount / letterCount;
    }

    private static string NormalizeForTitleComparison(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char character in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
            }
        }

        return Regex
            .Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ")
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Trim();
    }
}
