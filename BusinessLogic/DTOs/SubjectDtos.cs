namespace BusinessLogic.DTOs;

public sealed record SubjectListItemDto(
    int SubjectId,
    string Name,
    string? Description,
    int ChapterCount,
    int DocumentCount,
    DateTime CreatedAt);

public sealed record SubjectDetailsDto(
    int SubjectId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    IReadOnlyList<ChapterListItemDto> Chapters);

public sealed record SubjectUpsertDto(
    int? SubjectId,
    int UserId,
    string Name,
    string? Description);

public sealed record ChapterListItemDto(
    int ChapterId,
    int SubjectId,
    int ChapterNumber,
    string Title,
    string? Description,
    int DocumentCount,
    DateTime CreatedAt);

public sealed record ChapterUpsertDto(
    int? ChapterId,
    int SubjectId,
    int UserId,
    string Title,
    string? Description);
