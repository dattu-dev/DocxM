using BusinessObjects.Entities;

namespace DataAcessLayer.Repositories;

public interface ISubjectRepository
{
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<Subject?> GetSubjectByIdAsync(
        int subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> SubjectNameExistsAsync(
        string name,
        int userId,
        int? excludedSubjectId = null,
        CancellationToken cancellationToken = default);

    Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default);

    void DeleteSubject(Subject subject);

    Task<IReadOnlyList<SubjectPermission>> GetSubjectPermissionsAsync(
        int subjectId,
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<SubjectPermission?> GetSubjectPermissionAsync(
        int subjectId,
        int ownerUserId,
        int studentUserId,
        CancellationToken cancellationToken = default);

    Task AddSubjectPermissionAsync(SubjectPermission permission, CancellationToken cancellationToken = default);

    void DeleteSubjectPermission(SubjectPermission permission);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(int subjectId, int userId, CancellationToken cancellationToken = default);

    Task<Chapter?> GetChapterByIdAsync(int chapterId, int userId, CancellationToken cancellationToken = default);

    Task<int> GetNextChapterNumberAsync(int subjectId, CancellationToken cancellationToken = default);

    Task AddChapterAsync(Chapter chapter, CancellationToken cancellationToken = default);

    void DeleteChapter(Chapter chapter);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
