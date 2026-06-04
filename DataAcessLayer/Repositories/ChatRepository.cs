using BusinessObjects.Entities;
using BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer.Repositories;

public sealed class ChatRepository : IChatRepository
{
    private readonly AppDbContext _context;

    public ChatRepository(AppDbContext context)
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
            .Where(subject => subject.IsActive);

        if (ownerUserId.HasValue)
        {
            // Instructor chat với Subject mình sở hữu.
            query = query.Where(subject => subject.CreatedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            // Student chat với Subject được cấp quyền.
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

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Chapter> query = _context.Chapters
            .AsNoTracking()
            .Include(chapter => chapter.Subject)
            .Where(chapter => chapter.SubjectId == subjectId &&
                              chapter.Subject.IsActive);

        if (ownerUserId.HasValue)
        {
            query = query.Where(chapter => chapter.Subject.CreatedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            query = query.Where(chapter => chapter.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(chapter => false);
        }

        return await query
            .OrderBy(chapter => chapter.SortOrder)
            .ThenBy(chapter => chapter.ChapterNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetIndexedDocumentsAsync(
        int? subjectId,
        int? chapterId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Document> query = _context.Documents
            .AsNoTracking()
            .Include(document => document.Chapter)
            .Where(document => document.ProcessingStatus == DocumentProcessingStatus.Indexed.ToString() &&
                               document.ChunkCount > 0);

        if (ownerUserId.HasValue)
        {
            // Document ứng viên RAG của Instructor phải do chính Instructor upload.
            query = query.Where(document => document.UploadedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            // Document ứng viên RAG của Student phải thuộc Subject được cấp quyền.
            query = query.Where(document => document.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(document => false);
        }

        if (subjectId.HasValue)
        {
            query = query.Where(document => document.SubjectId == subjectId.Value);
        }

        if (chapterId.HasValue)
        {
            query = query.Where(document => document.ChapterId == chapterId.Value);
        }

        return await query
            .OrderBy(document => document.OriginalFileName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetDocumentsAsync(
        int? subjectId,
        int? chapterId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Document> query = _context.Documents
            .AsNoTracking()
            .Include(document => document.Chapter);

        if (ownerUserId.HasValue)
        {
            query = query.Where(document => document.UploadedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            query = query.Where(document => document.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(document => false);
        }

        if (subjectId.HasValue)
        {
            query = query.Where(document => document.SubjectId == subjectId.Value);
        }

        if (chapterId.HasValue)
        {
            query = query.Where(document => document.ChapterId == chapterId.Value);
        }

        return await query
            .OrderBy(document => document.OriginalFileName)
            .ToListAsync(cancellationToken);
    }

    public Task<Subject?> GetSubjectAsync(
        int subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Subject> query = _context.Subjects
            .AsNoTracking()
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

    public Task<Chapter?> GetChapterAsync(
        int chapterId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Chapter> query = _context.Chapters
            .AsNoTracking()
            .Include(chapter => chapter.Subject)
            .Where(chapter => chapter.ChapterId == chapterId &&
                              chapter.Subject.IsActive);

        if (ownerUserId.HasValue)
        {
            query = query.Where(chapter => chapter.Subject.CreatedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            query = query.Where(chapter => chapter.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(chapter => false);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Document?> GetDocumentAsync(
        int documentId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Document> query = _context.Documents
            .AsNoTracking()
            .Include(document => document.Chapter)
            .Where(document => document.DocumentId == documentId);

        if (ownerUserId.HasValue)
        {
            query = query.Where(document => document.UploadedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            query = query.Where(document => document.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(document => false);
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksByIdsAsync(
        IReadOnlyCollection<long> chunkIds,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (chunkIds.Count == 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        IQueryable<DocumentChunk> query = _context.DocumentChunks
            .AsNoTracking()
            .Include(chunk => chunk.Document)
                .ThenInclude(document => document.Chapter)
            .Where(chunk => chunkIds.Contains(chunk.DocumentChunkId));

        // Chunk truy xuất từ vector search vẫn được lọc lại bằng quyền database.
        if (ownerUserId.HasValue)
        {
            query = query.Where(chunk => chunk.Document.UploadedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            query = query.Where(chunk => chunk.Document.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(chunk => false);
        }

        return await query
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds,
        int? ownerUserId,
        int? viewerUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0 || take <= 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        IQueryable<DocumentChunk> query = _context.DocumentChunks
            .AsNoTracking()
            .Include(chunk => chunk.Document)
                .ThenInclude(document => document.Chapter)
            .Where(chunk => documentIds.Contains(chunk.DocumentId));

        if (ownerUserId.HasValue)
        {
            query = query.Where(chunk => chunk.Document.UploadedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            query = query.Where(chunk => chunk.Document.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            query = query.Where(chunk => false);
        }

        return await query
            .OrderBy(chunk => chunk.DocumentId)
            .ThenBy(chunk => chunk.ChunkIndex)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchChunksByKeywordsAsync(
        IReadOnlyCollection<int> documentIds,
        int? ownerUserId,
        int? viewerUserId,
        IReadOnlyCollection<string> keywords,
        int take,
        CancellationToken cancellationToken = default)
    {
        string[] normalizedKeywords = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        if (documentIds.Count == 0 || normalizedKeywords.Length == 0 || take <= 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        IQueryable<DocumentChunk> baseQuery = _context.DocumentChunks
            .AsNoTracking()
            .Include(chunk => chunk.Document)
                .ThenInclude(document => document.Chapter)
            .Where(chunk => documentIds.Contains(chunk.DocumentId));

        if (ownerUserId.HasValue)
        {
            baseQuery = baseQuery.Where(chunk => chunk.Document.UploadedByUserId == ownerUserId.Value);
        }
        else if (viewerUserId.HasValue)
        {
            baseQuery = baseQuery.Where(chunk => chunk.Document.Subject.SubjectPermissions.Any(
                permission => permission.StudentUserId == viewerUserId.Value));
        }
        else
        {
            baseQuery = baseQuery.Where(chunk => false);
        }

        var results = new List<DocumentChunk>();
        var seenChunkIds = new HashSet<long>();

        foreach (string keyword in normalizedKeywords)
        {
            string localKeyword = keyword;
            // Keyword search bổ sung ứng viên khi câu hỏi có từ khóa rõ ràng như chapter/tool.
            List<DocumentChunk> matches = await baseQuery
                .Where(chunk =>
                    chunk.Content.Contains(localKeyword) ||
                    (chunk.SectionTitle != null && chunk.SectionTitle.Contains(localKeyword)) ||
                    chunk.Document.Title.Contains(localKeyword) ||
                    chunk.Document.OriginalFileName.Contains(localKeyword) ||
                    (chunk.Document.Chapter != null && chunk.Document.Chapter.Title.Contains(localKeyword)))
                .OrderBy(chunk => chunk.DocumentId)
                .ThenBy(chunk => chunk.ChunkIndex)
                .Take(take)
                .ToListAsync(cancellationToken);

            foreach (DocumentChunk match in matches)
            {
                if (seenChunkIds.Add(match.DocumentChunkId))
                {
                    results.Add(match);
                }

                if (results.Count >= take)
                {
                    return results;
                }
            }
        }

        return results
            .OrderBy(chunk => chunk.DocumentId)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ToList();
    }

    public Task<ChatConversation?> GetConversationAsync(
        int userId,
        int? subjectId,
        int? chapterId,
        int? documentId,
        CancellationToken cancellationToken = default)
    {
        return _context.ChatConversations
            .FirstOrDefaultAsync(
                conversation => conversation.UserId == userId &&
                                conversation.SubjectId == subjectId &&
                                conversation.ChapterId == chapterId &&
                                conversation.DocumentId == documentId,
                cancellationToken);
    }

    public Task<ChatConversation?> GetConversationByIdAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Conversation id luôn đi kèm user id để không đọc lịch sử của tài khoản khác.
        return _context.ChatConversations
            .FirstOrDefaultAsync(
                conversation => conversation.ChatConversationId == conversationId &&
                                conversation.UserId == userId,
                cancellationToken);
    }

    public async Task<bool> DeleteConversationAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        ChatConversation? conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(
                conversation => conversation.ChatConversationId == conversationId &&
                                conversation.UserId == userId,
                cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        _context.ChatConversations.Remove(conversation);

        return true;
    }

    public async Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(
        int userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatConversations
            .AsNoTracking()
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ThenByDescending(conversation => conversation.ChatConversationId)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        int userId,
        int? subjectId,
        int? chapterId,
        int? documentId,
        int take,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ChatMessage> query = _context.ChatMessages
            .AsNoTracking()
            .Include(message => message.ChatConversation)
            .Include(message => message.ChatCitations)
            .Where(message => message.ChatConversation.UserId == userId);

        if (subjectId.HasValue)
        {
            query = query.Where(message => message.ChatConversation.SubjectId == subjectId.Value);
        }

        if (chapterId.HasValue)
        {
            query = query.Where(message => message.ChatConversation.ChapterId == chapterId.Value);
        }

        if (documentId.HasValue)
        {
            query = query.Where(message => message.ChatConversation.DocumentId == documentId.Value);
        }

        List<ChatMessage> messages = await query
            .OrderByDescending(message => message.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return messages
            .OrderBy(message => message.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesByConversationIdAsync(
        int conversationId,
        int userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        List<ChatMessage> messages = await _context.ChatMessages
            .AsNoTracking()
            .Include(message => message.ChatConversation)
            .Include(message => message.ChatCitations)
            .Where(message => message.ChatConversationId == conversationId &&
                              message.ChatConversation.UserId == userId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return messages
            .OrderBy(message => message.CreatedAt)
            .ToList();
    }

    public async Task AddConversationAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken = default)
    {
        await _context.ChatConversations.AddAsync(conversation, cancellationToken);
    }

    public async Task AddMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        await _context.ChatMessages.AddAsync(message, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
