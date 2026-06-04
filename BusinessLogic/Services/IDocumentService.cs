using BusinessLogic.DTOs;

namespace BusinessLogic.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentListItemDto>> GetDocumentsAsync(
        DocumentFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<DocumentDetailsDto?> GetDocumentDetailsAsync(
        int documentId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<DocumentFileDto?> GetDocumentFileAsync(
        int documentId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubjectOptionDto>> GetSubjectsAsync(
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChapterOptionDto>> GetChaptersAsync(
        int? subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<DocumentUploadResultDto> UploadDocumentAsync(
        DocumentUploadDto upload,
        CancellationToken cancellationToken = default);

    Task<DocumentUploadResultDto?> ReIndexDocumentAsync(
        int documentId,
        int userId,
        string webRootPath,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDocumentAsync(
        int documentId,
        int userId,
        string webRootPath,
        CancellationToken cancellationToken = default);
}
