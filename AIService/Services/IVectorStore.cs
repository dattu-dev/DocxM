using AIService.Models;

namespace AIService.Services;

public interface IVectorStore
{
    Task UpsertAsync(
        VectorRecord vector,
        string storageRootPath,
        CancellationToken cancellationToken = default);

    Task DeleteByDocumentIdAsync(
        int documentId,
        string storageRootPath,
        CancellationToken cancellationToken = default);
}
