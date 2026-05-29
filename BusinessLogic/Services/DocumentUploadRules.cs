namespace BusinessLogic.Services;

public static class DocumentUploadRules
{
    public const long MaxFileSizeBytes = 104_857_600;
    public const int MaxTitleLength = 250;
    public const int MaxDescriptionLength = 1000;
    public const int MaxOriginalFileNameLength = 260;

    public static readonly string[] AllowedExtensions =
    [
        ".pdf",
        ".docx",
        ".pptx"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> AllowedContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] =
            [
                "application/pdf",
                "application/x-pdf",
                "application/octet-stream"
            ],
            [".docx"] =
            [
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/octet-stream"
            ],
            [".pptx"] =
            [
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "application/vnd.ms-powerpoint",
                "application/octet-stream"
            ],
        };

    public static bool IsAllowedExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) &&
               AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsAllowedContentType(string extension, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        if (!AllowedContentTypes.TryGetValue(extension, out string[]? allowedTypes))
        {
            return false;
        }

        string normalizedContentType = contentType.Split(';', StringSplitOptions.TrimEntries)[0];

        return allowedTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase);
    }
}
