namespace BusinessLogic.DTOs;

public sealed record DocumentFilterDto(
    int? SubjectId,
    int? ChapterId,
    string? SearchTerm,
    int? UploadedByUserId = null);

public sealed record DocumentListItemDto(
    int DocumentId,
    string Title,
    string OriginalFileName,
    string ContentType,
    string FileExtension,
    long FileSizeBytes,
    string ProcessingStatus,
    int ChunkCount,
    DateTime UploadedAt,
    string SubjectName,
    string? ChapterTitle,
    string? UploadedByFullName);

public sealed record DocumentChunkDto(
    long DocumentChunkId,
    int ChunkIndex,
    string Content,
    int? TokenCount,
    int? PageNumber,
    string? VectorId);

public sealed record DocumentDetailsDto(
    int DocumentId,
    string Title,
    string? Description,
    string OriginalFileName,
    string StoredFileName,
    string StoragePath,
    string ContentType,
    string FileExtension,
    long FileSizeBytes,
    string ProcessingStatus,
    int ChunkCount,
    DateTime UploadedAt,
    string SubjectName,
    string? ChapterTitle,
    string? UploadedByFullName,
    IReadOnlyList<DocumentChunkDto> Chunks);

public sealed record DocumentFileDto(
    int DocumentId,
    string OriginalFileName,
    string StoragePath,
    string ContentType);

public sealed record DocumentUploadResultDto(
    int DocumentId,
    string ProcessingStatus,
    int ChunkCount);

public sealed record SubjectOptionDto(
    int SubjectId,
    string Code,
    string Name);

public sealed record ChapterOptionDto(
    int ChapterId,
    int SubjectId,
    int ChapterNumber,
    string Title);

public sealed class DocumentUploadDto
{
    public int UploadedByUserId { get; init; }

    public int SubjectId { get; init; }

    public string ChapterTitle { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string OriginalFileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public Stream FileStream { get; init; } = Stream.Null;

    public string WebRootPath { get; init; } = string.Empty;
}
