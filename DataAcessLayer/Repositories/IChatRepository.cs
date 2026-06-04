using BusinessObjects.Entities;

namespace DataAcessLayer.Repositories;

public interface IChatRepository
{
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetIndexedDocumentsAsync(
        int? subjectId,
        int? chapterId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetDocumentsAsync(
        int? subjectId,
        int? chapterId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<Subject?> GetSubjectAsync(
        int subjectId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<Chapter?> GetChapterAsync(
        int chapterId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<Document?> GetDocumentAsync(
        int documentId,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetChunksByIdsAsync(
        IReadOnlyCollection<long> chunkIds,
        int? ownerUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds,
        int? ownerUserId,
        int? viewerUserId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> SearchChunksByKeywordsAsync(
        IReadOnlyCollection<int> documentIds,
        int? ownerUserId,
        int? viewerUserId,
        IReadOnlyCollection<string> keywords,
        int take,
        CancellationToken cancellationToken = default);

    Task<ChatConversation?> GetConversationAsync(
        int userId,
        int? subjectId,
        int? chapterId,
        int? documentId,
        CancellationToken cancellationToken = default);

    Task<ChatConversation?> GetConversationByIdAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteConversationAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(
        int userId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        int userId,
        int? subjectId,
        int? chapterId,
        int? documentId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetMessagesByConversationIdAsync(
        int conversationId,
        int userId,
        int take,
        CancellationToken cancellationToken = default);

    Task AddConversationAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken = default);

    Task AddMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
