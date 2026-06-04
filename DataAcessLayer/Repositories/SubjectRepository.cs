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
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Subject> query = _context.Subjects
            .AsNoTracking()
            .Include(subject => subject.Chapters)
            .Include(subject => subject.Documents)
            .Where(subject => subject.IsActive);

        if (ownerUserId.HasValue)
        {
            // Instructor chỉ thấy Subject do mình tạo.
            query = query.Where(subject => subject.CreatedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            // Student chỉ thấy Subject được cấp quyền trong SubjectPermissions.
            query = query.Where(subject => subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(subject => false);
        }

        return await query
            .OrderBy(subject => subject.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Subject?> GetSubjectByIdAsync(
        int subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Subject> query = _context.Subjects
            .Include(subject => subject.Chapters)
                .ThenInclude(chapter => chapter.Documents)
            .Include(subject => subject.Documents)
            .Where(subject => subject.SubjectId == subjectId && subject.IsActive);

        if (ownerUserId.HasValue)
        {
            query = query.Where(subject => subject.CreatedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            query = query.Where(subject => subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(subject => false);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
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

    public async Task<IReadOnlyList<SubjectPermission>> GetSubjectPermissionsAsync(
        int subjectId,
        int ownerUserId,
        CancellationToken cancellationToken = default)
    {
        // Danh sách quyền chỉ mở cho Instructor sở hữu Subject.
        return await _context.SubjectPermissions
            .AsNoTracking()
            .Include(permission => permission.StudentUser)
            .Where(permission => permission.SubjectId == subjectId &&
                                 permission.Subject.CreatedByUserId == ownerUserId)
            .OrderBy(permission => permission.StudentUser.FullName)
            .ThenBy(permission => permission.StudentUser.Email)
            .ToListAsync(cancellationToken);
    }

    public Task<SubjectPermission?> GetSubjectPermissionAsync(
        int subjectId,
        int ownerUserId,
        int studentUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.SubjectPermissions
            .Include(permission => permission.Subject)
            .FirstOrDefaultAsync(
                permission => permission.SubjectId == subjectId &&
                              permission.Subject.CreatedByUserId == ownerUserId &&
                              permission.StudentUserId == studentUserId,
                cancellationToken);
    }

    public async Task AddSubjectPermissionAsync(
        SubjectPermission permission,
        CancellationToken cancellationToken = default)
    {
        await _context.SubjectPermissions.AddAsync(permission, cancellationToken);
    }

    public void DeleteSubjectPermission(SubjectPermission permission)
    {
        _context.SubjectPermissions.Remove(permission);
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
