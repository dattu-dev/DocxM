using BusinessObjects.Entities;

namespace DataAcessLayer.Repositories;

public interface ISubjectRepository
{
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(int userId, CancellationToken cancellationToken = default);

    Task<Subject?> GetSubjectByIdAsync(int subjectId, int userId, CancellationToken cancellationToken = default);

    Task<bool> SubjectNameExistsAsync(
        string name,
        int userId,
        int? excludedSubjectId = null,
        CancellationToken cancellationToken = default);

    Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default);

    void DeleteSubject(Subject subject);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(int subjectId, int userId, CancellationToken cancellationToken = default);

    Task<Chapter?> GetChapterByIdAsync(int chapterId, int userId, CancellationToken cancellationToken = default);

    Task<int> GetNextChapterNumberAsync(int subjectId, CancellationToken cancellationToken = default);

    Task AddChapterAsync(Chapter chapter, CancellationToken cancellationToken = default);

    void DeleteChapter(Chapter chapter);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
