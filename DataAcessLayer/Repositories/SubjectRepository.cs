using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer.Repositories;

public sealed class SubjectRepository : ISubjectRepository
{
    private readonly AppDbContext _context;

    public SubjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Subject>> GetSubjectsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Subjects
            .AsNoTracking()
            .Include(subject => subject.Chapters)
            .Include(subject => subject.Documents)
            .Where(subject => subject.CreatedByUserId == userId)
            .OrderBy(subject => subject.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Subject?> GetSubjectByIdAsync(
        int subjectId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Subjects
            .Include(subject => subject.Chapters)
                .ThenInclude(chapter => chapter.Documents)
            .Include(subject => subject.Documents)
            .FirstOrDefaultAsync(
                subject => subject.SubjectId == subjectId && subject.CreatedByUserId == userId,
                cancellationToken);
    }

    public Task<bool> SubjectNameExistsAsync(
        string name,
        int userId,
        int? excludedSubjectId = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim();

        return _context.Subjects
            .AsNoTracking()
            .AnyAsync(
                subject => subject.CreatedByUserId == userId &&
                           subject.Name == normalizedName &&
                           (!excludedSubjectId.HasValue || subject.SubjectId != excludedSubjectId.Value),
                cancellationToken);
    }

    public async Task AddSubjectAsync(Subject subject, CancellationToken cancellationToken = default)
    {
        await _context.Subjects.AddAsync(subject, cancellationToken);
    }

    public void DeleteSubject(Subject subject)
    {
        _context.Subjects.Remove(subject);
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int subjectId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Chapters
            .AsNoTracking()
            .Include(chapter => chapter.Subject)
            .Include(chapter => chapter.Documents)
            .Where(chapter => chapter.SubjectId == subjectId && chapter.Subject.CreatedByUserId == userId)
            .OrderBy(chapter => chapter.SortOrder)
            .ThenBy(chapter => chapter.ChapterNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<Chapter?> GetChapterByIdAsync(
        int chapterId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Chapters
            .Include(chapter => chapter.Subject)
            .Include(chapter => chapter.Documents)
            .FirstOrDefaultAsync(
                chapter => chapter.ChapterId == chapterId && chapter.Subject.CreatedByUserId == userId,
                cancellationToken);
    }

    public async Task<int> GetNextChapterNumberAsync(
        int subjectId,
        CancellationToken cancellationToken = default)
    {
        int? maxChapterNumber = await _context.Chapters
            .Where(chapter => chapter.SubjectId == subjectId)
            .MaxAsync(chapter => (int?)chapter.ChapterNumber, cancellationToken);

        return (maxChapterNumber ?? 0) + 1;
    }

    public async Task AddChapterAsync(Chapter chapter, CancellationToken cancellationToken = default)
    {
        await _context.Chapters.AddAsync(chapter, cancellationToken);
    }

    public void DeleteChapter(Chapter chapter)
    {
        _context.Chapters.Remove(chapter);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
