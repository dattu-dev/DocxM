namespace BusinessLogic.DTOs;

// Scope chat mang cả phạm vi tài liệu và filter quyền của user hiện tại.
public sealed record ChatScopeDto(
    int? SubjectId,
    int? ChapterId,
    int? DocumentId,
    int? ConversationId,
    int? OwnerUserId,
    int? ViewerUserId);

// Option document cho UI chat biết tài liệu nào đã sẵn sàng hỏi đáp.
public sealed record ChatDocumentOptionDto(
    int DocumentId,
    int SubjectId,
    int? ChapterId,
    string OriginalFileName,
    string Title,
    string ProcessingStatus,
    int ChunkCount,
    bool CanChat);

// Citation trả về UI để hiển thị nguồn RAG tách khỏi nội dung answer.
public sealed record ChatCitationDto(
    int DocumentId,
    long DocumentChunkId,
    string DocumentName,
    int? PageNumber,
    string Snippet,
    double SimilarityScore);

public sealed record ChatMessageDto(
    long ChatMessageId,
    string UserQuestion,
    string AiAnswer,
    DateTime CreatedAt,
    IReadOnlyList<ChatCitationDto> Citations);

public sealed record ChatConversationListItemDto(
    int ChatConversationId,
    string Title,
    int? SubjectId,
    int? ChapterId,
    int? DocumentId,
    DateTime UpdatedAt);

public sealed record ChatPageDto(
    IReadOnlyList<SubjectOptionDto> Subjects,
    IReadOnlyList<ChapterOptionDto> Chapters,
    IReadOnlyList<ChatDocumentOptionDto> Documents,
    IReadOnlyList<ChatConversationListItemDto> Conversations,
    IReadOnlyList<ChatMessageDto> Messages,
    int? CurrentConversationId,
    int? CurrentSubjectId,
    int? CurrentChapterId,
    int? CurrentDocumentId,
    bool HasIndexedDocuments);

// DTO này là đầu vào chính của ChatService.AskAsync.
public sealed record ChatAskDto(
    int UserId,
    int? ConversationId,
    int? SubjectId,
    int? ChapterId,
    int? DocumentId,
    string Question,
    string WebRootPath,
    int? OwnerUserId,
    int? ViewerUserId);

public sealed record ChatAnswerDto(
    int ConversationId,
    string Question,
    string Answer,
    DateTime CreatedAt,
    IReadOnlyList<ChatCitationDto> Citations);
