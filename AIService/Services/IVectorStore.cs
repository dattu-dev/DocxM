using AIService.Models;

namespace AIService.Services;

public interface IVectorStore
{
    Task UpsertAsync(
        VectorRecord vector,
        string storageRootPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        string storageRootPath,
        IReadOnlyCollection<int> documentIds,
        int topK,
        CancellationToken cancellationToken = default);

    Task DeleteByDocumentIdAsync(
        int documentId,
        string storageRootPath,
        CancellationToken cancellationToken = default);
}
