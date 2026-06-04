using BusinessLogic.DTOs;

namespace BusinessLogic.Services;

public interface ISubjectService
{
    Task<IReadOnlyList<SubjectListItemDto>> GetSubjectsAsync(
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<SubjectDetailsDto?> GetSubjectDetailsAsync(
        int subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<int> CreateSubjectAsync(
        SubjectUpsertDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateSubjectAsync(
        SubjectUpsertDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSubjectAsync(
        int subjectId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<SubjectPermissionsDto?> GetSubjectPermissionsAsync(
        int subjectId,
        int instructorUserId,
        CancellationToken cancellationToken = default);

    Task<bool> GrantSubjectPermissionAsync(
        SubjectPermissionGrantDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeSubjectPermissionAsync(
        int subjectId,
        int instructorUserId,
        int studentUserId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteChapterAsync(
        int subjectId,
        int chapterId,
        int userId,
        CancellationToken cancellationToken = default);
}
