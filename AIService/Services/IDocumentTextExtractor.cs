namespace AIService.Services;

public interface IDocumentTextExtractor
{
    bool CanExtract(string fileExtension);

    Task<string> ExtractTextAsync(
        string filePath,
        string fileExtension,
        CancellationToken cancellationToken = default);
}
