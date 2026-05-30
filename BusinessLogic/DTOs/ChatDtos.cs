namespace BusinessLogic.DTOs;

public sealed record ChatScopeDto(
    int? SubjectId,
    int? ChapterId,
    int? DocumentId,
    int? ConversationId);

public sealed record ChatDocumentOptionDto(
    int DocumentId,
    int SubjectId,
    int? ChapterId,
    string OriginalFileName,
    string Title,
    string ProcessingStatus,
    int ChunkCount,
    bool CanChat);

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

public sealed record ChatAskDto(
    int UserId,
    int? ConversationId,
    int? SubjectId,
    int? ChapterId,
    int? DocumentId,
    string Question,
    string WebRootPath);

public sealed record ChatAnswerDto(
    int ConversationId,
    string Question,
    string Answer,
    DateTime CreatedAt,
    IReadOnlyList<ChatCitationDto> Citations);
