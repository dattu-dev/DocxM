using BusinessObjects.Entities;

namespace DataAcessLayer.Repositories;

public interface IDocumentRepository
{
    Task<IReadOnlyList<Document>> GetDocumentsAsync(
        int? subjectId,
        int? chapterId,
        string? searchTerm,
        int? uploadedByUserId,
        CancellationToken cancellationToken = default);

    Task<Document?> GetDocumentByIdAsync(
        int documentId,
        bool includeChunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subject>> GetSubjectsAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int? subjectId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> SubjectExistsAsync(int subjectId, int userId, CancellationToken cancellationToken = default);

    Task<Chapter?> GetChapterByIdAsync(int chapterId, int userId, CancellationToken cancellationToken = default);

    Task<Chapter> GetOrCreateChapterAsync(
        int subjectId,
        int userId,
        string title,
        CancellationToken cancellationToken = default);

    Task AddDocumentAsync(Document document, CancellationToken cancellationToken = default);

    void DeleteDocument(Document document);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
