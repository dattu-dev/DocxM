using BusinessObjects.Entities;

namespace BusinessLogic.Services;

public interface IDocumentProcessingService
{
    Task<DocumentTextProcessingResult> ExtractAndChunkAsync(
        string physicalPath,
        string fileExtension,
        string originalFileName,
        string currentTitle,
        CancellationToken cancellationToken = default);

    Task<DocumentVectorProcessingResult> StoreVectorsIfReadyAsync(
        Document document,
        string storageRootPath,
        CancellationToken cancellationToken = default);

    Task DeleteVectorsIfExistsAsync(
        int documentId,
        string storageRootPath,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentTextProcessingResult(
    string Title,
    string ProcessingStatus,
    string? ErrorMessage,
    IReadOnlyList<DocumentChunkProcessingResult> Chunks);

public sealed record DocumentChunkProcessingResult(
    int ChunkIndex,
    string Content,
    int? TokenCount,
    string? SectionTitle,
    string VectorId,
    DateTime CreatedAt);

public sealed record DocumentVectorProcessingResult(
    string ProcessingStatus,
    string? ErrorMessage,
    DateTime? UpdatedAt);
