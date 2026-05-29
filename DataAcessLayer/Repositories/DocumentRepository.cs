using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer.Repositories;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _context;

    public DocumentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Document>> GetDocumentsAsync(
        int? subjectId,
        int? chapterId,
        string? searchTerm,
        int? uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Document> query = _context.Documents
            .AsNoTracking()
            .Include(document => document.Subject)
            .Include(document => document.Chapter)
            .Include(document => document.UploadedByUser);

        if (subjectId.HasValue)
        {
            query = query.Where(document => document.SubjectId == subjectId.Value);
        }

        if (chapterId.HasValue)
        {
            query = query.Where(document => document.ChapterId == chapterId.Value);
        }

        if (uploadedByUserId.HasValue)
        {
            query = query.Where(document => document.UploadedByUserId == uploadedByUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string keyword = searchTerm.Trim();
            query = query.Where(document =>
                document.Title.Contains(keyword) ||
                document.OriginalFileName.Contains(keyword) ||
                (document.Description != null && document.Description.Contains(keyword)));
        }

        return await query
            .OrderByDescending(document => document.UploadedAt)
            .ThenByDescending(document => document.DocumentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetDocumentByIdAsync(
        int documentId,
        bool includeChunks,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Document> query = _context.Documents
            .Include(document => document.Subject)
            .Include(document => document.Chapter)
            .Include(document => document.UploadedByUser);

        if (includeChunks)
        {
            query = query.Include(document => document.DocumentChunks);
        }

        return await query.FirstOrDefaultAsync(
            document => document.DocumentId == documentId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Subject>> GetSubjectsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Subjects
            .AsNoTracking()
            .Where(subject => subject.IsActive && subject.CreatedByUserId == userId)
            .OrderBy(subject => subject.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int? subjectId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Chapter> query = _context.Chapters
            .AsNoTracking()
            .Include(chapter => chapter.Subject)
            .Where(chapter => chapter.Subject.CreatedByUserId == userId);

        if (subjectId.HasValue)
        {
            query = query.Where(chapter => chapter.SubjectId == subjectId.Value);
        }

        return await query
            .OrderBy(chapter => chapter.Subject.Code)
            .ThenBy(chapter => chapter.SortOrder)
            .ThenBy(chapter => chapter.ChapterNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> SubjectExistsAsync(
        int subjectId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Subjects
            .AsNoTracking()
            .AnyAsync(
                subject => subject.SubjectId == subjectId &&
                           subject.CreatedByUserId == userId &&
                           subject.IsActive,
                cancellationToken);
    }

    public Task<Chapter?> GetChapterByIdAsync(
        int chapterId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Chapters
            .AsNoTracking()
            .Include(chapter => chapter.Subject)
            .FirstOrDefaultAsync(
                chapter => chapter.ChapterId == chapterId &&
                           chapter.Subject.CreatedByUserId == userId,
                cancellationToken);
    }

    public async Task AddDocumentAsync(Document document, CancellationToken cancellationToken = default)
    {
        await _context.Documents.AddAsync(document, cancellationToken);
    }

    public void DeleteDocument(Document document)
    {
        _context.Documents.Remove(document);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
