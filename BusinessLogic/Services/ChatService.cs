using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AIService.Models;
using AIService.Services;
using BusinessLogic.DTOs;
using BusinessLogic.Validation;
using BusinessObjects.Entities;
using BusinessObjects.Enums;
using DataAcessLayer.Repositories;

namespace BusinessLogic.Services;

public sealed class ChatService : IChatService
{
    private const int MaxQuestionLength = 1000;
    private const int MaxHistoryItems = 30;
    private const int MaxConversations = 25;
    private const int VectorCandidateChunks = 10;
    private const int MaxKeywordCandidateChunks = 500;
    private const int TopRelevantChunks = 5;
    private const int MaxCitationSources = 3;
    private const int MaxPromptChunkCharacters = 2400;
    private const int MaxSnippetLength = 180;

    private enum ChatQuestionIntent
    {
        Metadata,
        ChapterTitle,
        Tool,
        Character,
        Content
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "la", "gi", "cua", "va", "hoac", "the", "nao", "trong", "cho", "toi", "hay", "neu", "mot", "cac", "nhung",
        "duoc", "ve", "voi", "tu", "den", "khi", "co", "khong", "document", "file", "can", "dung", "lam", "do",
        "nao", "hay", "cho", "biet"
    };

    private readonly IChatRepository _chatRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IAnswerGenerationService _answerGenerationService;

    public ChatService(
        IChatRepository chatRepository,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IAnswerGenerationService answerGenerationService)
    {
        _chatRepository = chatRepository;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _answerGenerationService = answerGenerationService;
    }

    public async Task<ChatPageDto> GetChatPageAsync(
        int userId,
        ChatScopeDto scope,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Subject> subjects = await _chatRepository.GetSubjectsAsync(userId, cancellationToken);
        ChatConversation? currentConversation = scope.ConversationId.HasValue
            ? await _chatRepository.GetConversationByIdAsync(scope.ConversationId.Value, userId, cancellationToken)
            : null;
        int? requestedSubjectId = currentConversation?.SubjectId ?? scope.SubjectId;
        int? requestedChapterId = currentConversation?.ChapterId ?? scope.ChapterId;
        int? requestedDocumentId = currentConversation?.DocumentId ?? scope.DocumentId;

        int? subjectId = subjects.Any(subject => subject.SubjectId == requestedSubjectId)
            ? requestedSubjectId
            : null;

        IReadOnlyList<Chapter> chapters = subjectId.HasValue
            ? await _chatRepository.GetChaptersAsync(subjectId.Value, userId, cancellationToken)
            : Array.Empty<Chapter>();
        int? chapterId = chapters.Any(chapter => chapter.ChapterId == requestedChapterId)
            ? requestedChapterId
            : null;

        IReadOnlyList<Document> documents = subjectId.HasValue
            ? await _chatRepository.GetDocumentsAsync(subjectId, chapterId, userId, cancellationToken)
            : Array.Empty<Document>();
        int? documentId = documents.Any(document => document.DocumentId == requestedDocumentId)
            ? requestedDocumentId
            : null;

        IReadOnlyList<ChatMessage> messages = currentConversation is null
            ? Array.Empty<ChatMessage>()
            : await _chatRepository.GetMessagesByConversationIdAsync(
                currentConversation.ChatConversationId,
                userId,
                MaxHistoryItems,
                cancellationToken);

        IReadOnlyList<Document> allIndexedDocuments = await _chatRepository.GetIndexedDocumentsAsync(
            null,
            null,
            userId,
            cancellationToken);
        IReadOnlyList<ChatConversation> conversations = await _chatRepository.GetConversationsAsync(
            userId,
            MaxConversations,
            cancellationToken);

        return new ChatPageDto(
            subjects.Select(subject => new SubjectOptionDto(subject.SubjectId, subject.Code, subject.Name)).ToList(),
            chapters.Select(chapter => new ChapterOptionDto(
                chapter.ChapterId,
                chapter.SubjectId,
                chapter.ChapterNumber,
                chapter.Title)).ToList(),
            documents.Select(MapDocumentOption).ToList(),
            conversations.Select(MapConversation).ToList(),
            messages.Select(MapMessage).ToList(),
            currentConversation?.ChatConversationId,
            subjectId,
            chapterId,
            documentId,
            allIndexedDocuments.Count > 0);
    }

    public async Task<ChatAnswerDto> AskAsync(
        ChatAskDto dto,
        CancellationToken cancellationToken = default)
    {
        string question = ValidateQuestion(dto.Question);

        Subject subject = await ValidateSubjectAsync(dto.SubjectId, dto.UserId, cancellationToken);
        Chapter? chapter = await ValidateChapterAsync(dto.ChapterId, subject.SubjectId, dto.UserId, cancellationToken);
        Document? selectedDocument = await ValidateDocumentAsync(
            dto.DocumentId,
            subject.SubjectId,
            chapter?.ChapterId,
            dto.UserId,
            cancellationToken);

        IReadOnlyList<Document> candidateDocuments = selectedDocument is not null
            ? new[] { selectedDocument }
            : await _chatRepository.GetIndexedDocumentsAsync(
                subject.SubjectId,
                chapter?.ChapterId,
                dto.UserId,
                cancellationToken);

        if (candidateDocuments.Count == 0)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(string.Empty, "Không có tài liệu đã xử lý trong phạm vi đã chọn. Hãy tải lên và xử lý tài liệu trước khi dùng chatbot.")
            ]);
        }

        ChatQuestionIntent intent = DetectIntent(question);
        string rewrittenQuery = RewriteSearchQuery(question, intent);
        ChatAnswerDto? metadataAnswer = TryAnswerMetadataQuestion(question, candidateDocuments);

        if (metadataAnswer is not null)
        {
            WriteRagDebug(
                question,
                intent,
                rewrittenQuery,
                Array.Empty<RankedChunk>(),
                Array.Empty<RankedChunk>(),
                "Metadata intent: không dùng vector search.",
                metadataAnswer.Answer);

            return await SaveChatAnswerAsync(
                dto,
                subject,
                chapter,
                selectedDocument,
                question,
                metadataAnswer.Answer,
                metadataAnswer.Citations,
                cancellationToken);
        }

        IReadOnlyList<RankedChunk> rankedChunks = await FindRelevantChunksAsync(
            question,
            rewrittenQuery,
            intent,
            candidateDocuments.Select(document => document.DocumentId).ToArray(),
            dto.UserId,
            dto.WebRootPath,
            cancellationToken);

        IReadOnlyList<RankedChunk> selectedChunks = SelectContextChunks(intent, rankedChunks);
        IReadOnlyList<ChatCitationDto> citations = selectedChunks
            .Select(MapCitation)
            .GroupBy(citation => new { citation.DocumentId, citation.PageNumber })
            .Select(group => group.OrderByDescending(citation => citation.SimilarityScore).First())
            .Take(MaxCitationSources)
            .ToList();
        string prompt = BuildPrompt(question, selectedChunks);
        string answer = selectedChunks.Count == 0
            ? "Tài liệu chưa có thông tin phù hợp."
            : await _answerGenerationService.GenerateAnswerAsync(prompt, cancellationToken);

        WriteRagDebug(
            question,
            intent,
            rewrittenQuery,
            rankedChunks,
            selectedChunks,
            prompt,
            answer);

        return await SaveChatAnswerAsync(
            dto,
            subject,
            chapter,
            selectedDocument,
            question,
            answer,
            citations,
            cancellationToken);
    }

    public async Task<bool> DeleteConversationAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId <= 0 || userId <= 0)
        {
            return false;
        }

        bool deleted = await _chatRepository.DeleteConversationAsync(
            conversationId,
            userId,
            cancellationToken);

        if (!deleted)
        {
            return false;
        }

        await _chatRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<ChatAnswerDto> SaveChatAnswerAsync(
        ChatAskDto dto,
        Subject subject,
        Chapter? chapter,
        Document? selectedDocument,
        string question,
        string answer,
        IReadOnlyList<ChatCitationDto> citations,
        CancellationToken cancellationToken)
    {
        DateTime createdAt = DateTime.UtcNow;
        ChatConversation conversation = await GetOrCreateConversationAsync(
            dto.ConversationId,
            dto.UserId,
            subject,
            chapter,
            selectedDocument,
            question,
            cancellationToken);

        conversation.UpdatedAt = createdAt;

        var message = new ChatMessage
        {
            ChatConversation = conversation,
            UserQuestion = question,
            AiAnswer = answer,
            CreatedAt = createdAt
        };

        foreach (ChatCitationDto citation in citations)
        {
            message.ChatCitations.Add(new ChatCitation
            {
                DocumentId = citation.DocumentId,
                DocumentChunkId = citation.DocumentChunkId,
                DocumentName = citation.DocumentName,
                PageNumber = citation.PageNumber,
                Snippet = citation.Snippet,
                SimilarityScore = citation.SimilarityScore
            });
        }

        await _chatRepository.AddMessageAsync(message, cancellationToken);
        await _chatRepository.SaveChangesAsync(cancellationToken);

        return new ChatAnswerDto(conversation.ChatConversationId, question, answer, createdAt, citations);
    }

    private static ChatAnswerDto? TryAnswerMetadataQuestion(
        string question,
        IReadOnlyList<Document> candidateDocuments)
    {
        string normalizedQuestion = NormalizeForSearch(question);

        if (!IsMetadataQuestion(normalizedQuestion))
        {
            return null;
        }

        Document document = candidateDocuments.Count == 1
            ? candidateDocuments[0]
            : SelectBestMetadataDocument(normalizedQuestion, candidateDocuments);

        string answer = BuildMetadataAnswer(normalizedQuestion, document);
        IReadOnlyList<ChatCitationDto> citations =
        [
            new ChatCitationDto(
                document.DocumentId,
                0,
                document.OriginalFileName,
                null,
                $"Tiêu đề: {document.Title}; File: {document.OriginalFileName}",
                1)
        ];

        return new ChatAnswerDto(0, question, answer, DateTime.UtcNow, citations);
    }

    private static ChatQuestionIntent DetectIntent(string question)
    {
        string normalizedQuestion = NormalizeForSearch(question);

        if (IsMetadataQuestion(normalizedQuestion))
        {
            return ChatQuestionIntent.Metadata;
        }

        if (IsChapterTitleQuestion(normalizedQuestion))
        {
            return ChatQuestionIntent.ChapterTitle;
        }

        if (IsToolQuestion(normalizedQuestion))
        {
            return ChatQuestionIntent.Tool;
        }

        if (IsCharacterQuestion(normalizedQuestion))
        {
            return ChatQuestionIntent.Character;
        }

        return ChatQuestionIntent.Content;
    }

    private static string RewriteSearchQuery(string question, ChatQuestionIntent intent)
    {
        string normalizedQuestion = NormalizeForSearch(question);

        return intent switch
        {
            ChatQuestionIntent.ChapterTitle => RewriteChapterTitleQuery(question, normalizedQuestion),
            ChatQuestionIntent.Tool => $"{question} Godot Unity GDScript C# Blender engine framework tool phần mềm công cụ làm game",
            ChatQuestionIntent.Character => $"{question} nhân vật chính protagonist main character Lượm",
            _ => question
        };
    }

    private static string RewriteChapterTitleQuery(string question, string normalizedQuestion)
    {
        Match chapterMatch = Regex.Match(
            normalizedQuestion,
            @"\b(?:chapter|chuong)\s+(?<number>\d+)\b",
            RegexOptions.IgnoreCase);

        if (!chapterMatch.Success)
        {
            return question;
        }

        string chapterNumber = chapterMatch.Groups["number"].Value;

        return $"{question} Chapter {chapterNumber} Chương {chapterNumber} tên chapter tiêu đề chương";
    }

    private static IReadOnlyList<RankedChunk> SelectContextChunks(
        ChatQuestionIntent intent,
        IReadOnlyList<RankedChunk> rankedChunks)
    {
        if (rankedChunks.Count == 0)
        {
            return Array.Empty<RankedChunk>();
        }

        if (intent is ChatQuestionIntent.ChapterTitle or ChatQuestionIntent.Tool or ChatQuestionIntent.Character &&
            rankedChunks[0].LexicalScore >= 1)
        {
            return rankedChunks.Take(2).ToList();
        }

        return rankedChunks.Take(TopRelevantChunks).ToList();
    }

    private static bool IsMetadataQuestion(string normalizedQuestion)
    {
        string[] metadataTerms =
        [
            "ten du an",
            "ten project",
            "project ten",
            "du an nay la gi",
            "ten de tai",
            "de tai nay la gi",
            "ten tai lieu",
            "tai lieu ten",
            "ten file",
            "file ten",
            "header",
            "tieu de tai lieu",
            "document title"
        ];

        return metadataTerms.Any(term => normalizedQuestion.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsChapterTitleQuestion(string normalizedQuestion)
    {
        return normalizedQuestion.Contains("ten", StringComparison.OrdinalIgnoreCase) &&
               Regex.IsMatch(
                   normalizedQuestion,
                   @"\b(?:chapter|chuong)\s+\d+\b",
                   RegexOptions.IgnoreCase);
    }

    private static bool IsCharacterQuestion(string normalizedQuestion)
    {
        return normalizedQuestion.Contains("nhan vat", StringComparison.OrdinalIgnoreCase) &&
               (normalizedQuestion.Contains("chinh", StringComparison.OrdinalIgnoreCase) ||
                normalizedQuestion.Contains("la ai", StringComparison.OrdinalIgnoreCase) ||
                normalizedQuestion.Contains("ai", StringComparison.OrdinalIgnoreCase));
    }

    private static Document SelectBestMetadataDocument(
        string normalizedQuestion,
        IReadOnlyList<Document> candidateDocuments)
    {
        return candidateDocuments
            .OrderByDescending(document =>
                CalculateMetadataMatchScore(normalizedQuestion, document.Title) +
                CalculateMetadataMatchScore(normalizedQuestion, document.OriginalFileName))
            .ThenByDescending(document => document.UpdatedAt ?? document.UploadedAt)
            .First();
    }

    private static int CalculateMetadataMatchScore(string normalizedQuestion, string value)
    {
        string normalizedValue = NormalizeForSearch(value);

        return normalizedValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(term => normalizedQuestion.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildMetadataAnswer(string normalizedQuestion, Document document)
    {
        bool asksFileName = normalizedQuestion.Contains("file", StringComparison.OrdinalIgnoreCase);
        bool asksDocumentName = normalizedQuestion.Contains("tai lieu", StringComparison.OrdinalIgnoreCase) ||
                                normalizedQuestion.Contains("document", StringComparison.OrdinalIgnoreCase) ||
                                normalizedQuestion.Contains("tieu de", StringComparison.OrdinalIgnoreCase);

        if (asksFileName)
        {
            return $"Tên file là {document.OriginalFileName}.";
        }

        if (asksDocumentName)
        {
            return $"Tên tài liệu là {document.Title}.";
        }

        return $"Dự án có tên là {document.Title}.";
    }

    private async Task<IReadOnlyList<RankedChunk>> FindRelevantChunksAsync(
        string question,
        string rewrittenQuery,
        ChatQuestionIntent intent,
        IReadOnlyCollection<int> documentIds,
        int userId,
        string webRootPath,
        CancellationToken cancellationToken)
    {
        string[] questionTerms = ExtractSearchTerms($"{question} {rewrittenQuery}");
        string[] keywordSearchTerms = BuildKeywordSearchTerms(question, rewrittenQuery, questionTerms, intent);
        float[] questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            rewrittenQuery,
            EmbeddingTaskType.RetrievalQuery,
            cancellationToken);
        IReadOnlyList<VectorSearchResult> vectorResults = await _vectorStore.SearchAsync(
            questionEmbedding,
            webRootPath,
            documentIds,
            VectorCandidateChunks,
            cancellationToken);

        Dictionary<long, double> vectorScores = vectorResults
            .GroupBy(result => result.Vector.ChunkId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(result => result.Score));

        IReadOnlyList<DocumentChunk> vectorChunks = await _chatRepository.GetChunksByIdsAsync(
            vectorScores.Keys.ToArray(),
            userId,
            cancellationToken);

        IReadOnlyList<DocumentChunk> keywordChunks = await _chatRepository.SearchChunksByKeywordsAsync(
            documentIds,
            userId,
            keywordSearchTerms,
            MaxKeywordCandidateChunks,
            cancellationToken);
        IReadOnlyList<DocumentChunk> chunks = vectorChunks
            .Concat(keywordChunks)
            .GroupBy(chunk => chunk.DocumentChunkId)
            .Select(group => group.First())
            .ToList();

        IReadOnlyList<RankedChunk> rankedChunks = RankChunks(chunks, vectorScores, questionTerms)
            .Where(chunk => chunk.LexicalScore > 0 || chunk.NormalizedVectorScore >= 0.78)
            .Take(VectorCandidateChunks)
            .ToList();

        return rankedChunks;
    }

    private static string BuildPrompt(
        string question,
        IReadOnlyList<RankedChunk> chunks)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("Bạn là trợ lý hỏi đáp tài liệu.");
        prompt.AppendLine("Nhiệm vụ của bạn là trả lời đúng câu hỏi của người dùng dựa trên context được cung cấp.");
        prompt.AppendLine("Không tóm tắt toàn bộ tài liệu nếu người dùng không yêu cầu.");
        prompt.AppendLine("Không chép nguyên văn context.");
        prompt.AppendLine("Nếu câu hỏi yêu cầu thông tin ngắn như tên dự án, tên chapter, tên nhân vật, công cụ, ngày tháng, hãy trả lời trực tiếp và ngắn gọn.");
        prompt.AppendLine("Nếu context không có đáp án, hãy trả lời: \"Tài liệu chưa có thông tin phù hợp.\"");
        prompt.AppendLine();
        prompt.AppendLine("Question:");
        prompt.AppendLine(question);
        prompt.AppendLine();
        prompt.AppendLine("Context:");

        for (int i = 0; i < chunks.Count; i++)
        {
            RankedChunk chunk = chunks[i];
            Document document = chunk.Chunk.Document;
            string page = chunk.Chunk.PageNumber.HasValue
                ? $" - Trang {chunk.Chunk.PageNumber.Value}"
                : string.Empty;
            string section = string.IsNullOrWhiteSpace(chunk.Chunk.SectionTitle)
                ? string.Empty
                : $" | Section: {chunk.Chunk.SectionTitle}";

            prompt.AppendLine($"[Nguồn {i + 1}: DocumentId: {document.DocumentId} | Title: {document.Title} | File: {document.OriginalFileName} | SubjectId: {document.SubjectId} | ChapterId: {document.ChapterId} | ChunkIndex: {chunk.Chunk.ChunkIndex}{page}{section}]");
            prompt.AppendLine(TrimForPrompt(chunk.Chunk.Content));
            prompt.AppendLine();
        }

        prompt.AppendLine("Answer:");
        prompt.AppendLine("Trả lời ngắn gọn, đúng trọng tâm.");
        prompt.AppendLine("- Trả lời bằng tiếng Việt.");
        prompt.AppendLine("- Không liệt kê toàn bộ nội dung tài liệu.");
        prompt.AppendLine("- Không bịa thông tin ngoài context.");
        prompt.AppendLine("- Trả lời tối đa 2 câu.");
        prompt.AppendLine("- Không ghi nguồn trong nội dung câu trả lời vì hệ thống sẽ hiển thị nguồn riêng.");

        return prompt.ToString();
    }

    private static string TrimForPrompt(string content)
    {
        string normalizedContent = NormalizeWhitespace(content);

        return normalizedContent.Length <= MaxPromptChunkCharacters
            ? normalizedContent
            : normalizedContent[..MaxPromptChunkCharacters].Trim() + "...";
    }

    private static void WriteRagDebug(
        string question,
        ChatQuestionIntent intent,
        string rewrittenQuery,
        IReadOnlyList<RankedChunk> retrievedChunks,
        IReadOnlyList<RankedChunk> selectedChunks,
        string finalContext,
        string finalAnswer)
    {
        var debug = new StringBuilder();
        debug.AppendLine("[RAG DEBUG] Question:");
        debug.AppendLine(question);
        debug.AppendLine($"[RAG DEBUG] Detected intent: {intent}");
        debug.AppendLine("[RAG DEBUG] Rewritten search query:");
        debug.AppendLine(rewrittenQuery);
        debug.AppendLine("[RAG DEBUG] Top chunks retrieved after hybrid rerank:");

        for (int i = 0; i < retrievedChunks.Count; i++)
        {
            RankedChunk chunk = retrievedChunks[i];
            debug.AppendLine(
                $"#{i + 1} DocumentId={chunk.Chunk.DocumentId}, File={chunk.Chunk.Document.OriginalFileName}, Title={chunk.Chunk.Document.Title}, ChunkId={chunk.Chunk.DocumentChunkId}, ChunkIndex={chunk.Chunk.ChunkIndex}, " +
                $"SectionTitle={chunk.Chunk.SectionTitle ?? "(none)"}, FinalScore={chunk.FinalScore:0.0000}, " +
                $"LexicalScore={chunk.LexicalScore:0.0000}, VectorScore={chunk.VectorScore:0.0000}, NormalizedVectorScore={chunk.NormalizedVectorScore:0.0000}");
            debug.AppendLine(BuildSnippet(chunk.Chunk.Content));
        }

        debug.AppendLine("[RAG DEBUG] Context chunks finally sent to AI:");

        for (int i = 0; i < selectedChunks.Count; i++)
        {
            RankedChunk chunk = selectedChunks[i];
            debug.AppendLine(
                $"#{i + 1} DocumentId={chunk.Chunk.DocumentId}, File={chunk.Chunk.Document.OriginalFileName}, Title={chunk.Chunk.Document.Title}, ChunkId={chunk.Chunk.DocumentChunkId}, ChunkIndex={chunk.Chunk.ChunkIndex}, " +
                $"FinalScore={chunk.FinalScore:0.0000}");
            debug.AppendLine(BuildSnippet(chunk.Chunk.Content));
        }

        debug.AppendLine("[RAG DEBUG] Final context/prompt sent to AI:");
        debug.AppendLine(finalContext);
        debug.AppendLine("[RAG DEBUG] Final answer:");
        debug.AppendLine(finalAnswer);

        Console.WriteLine(debug.ToString());
        System.Diagnostics.Debug.WriteLine(debug.ToString());
    }

    private async Task<ChatConversation> GetOrCreateConversationAsync(
        int? conversationId,
        int userId,
        Subject subject,
        Chapter? chapter,
        Document? document,
        string question,
        CancellationToken cancellationToken)
    {
        if (conversationId.HasValue)
        {
            ChatConversation? existingConversation = await _chatRepository.GetConversationByIdAsync(
                conversationId.Value,
                userId,
                cancellationToken);

            if (existingConversation is not null)
            {
                return existingConversation;
            }
        }

        var conversation = new ChatConversation
        {
            UserId = userId,
            SubjectId = subject.SubjectId,
            ChapterId = chapter?.ChapterId,
            DocumentId = document?.DocumentId,
            Title = BuildConversationTitle(question, subject, chapter, document),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _chatRepository.AddConversationAsync(conversation, cancellationToken);

        return conversation;
    }

    private async Task<Subject> ValidateSubjectAsync(
        int? subjectId,
        int userId,
        CancellationToken cancellationToken)
    {
        if (!subjectId.HasValue || subjectId.Value <= 0)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(ChatAskDto.SubjectId), "Vui lòng chọn môn học.")
            ]);
        }

        Subject? subject = await _chatRepository.GetSubjectAsync(subjectId.Value, userId, cancellationToken);

        if (subject is null)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(ChatAskDto.SubjectId), "Môn học không tồn tại hoặc không thuộc tài khoản của bạn.")
            ]);
        }

        return subject;
    }

    private async Task<Chapter?> ValidateChapterAsync(
        int? chapterId,
        int subjectId,
        int userId,
        CancellationToken cancellationToken)
    {
        if (!chapterId.HasValue || chapterId.Value <= 0)
        {
            return null;
        }

        Chapter? chapter = await _chatRepository.GetChapterAsync(chapterId.Value, userId, cancellationToken);

        if (chapter is null || chapter.SubjectId != subjectId)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(ChatAskDto.ChapterId), "Chương không thuộc môn học đã chọn.")
            ]);
        }

        return chapter;
    }

    private async Task<Document?> ValidateDocumentAsync(
        int? documentId,
        int subjectId,
        int? chapterId,
        int userId,
        CancellationToken cancellationToken)
    {
        if (!documentId.HasValue || documentId.Value <= 0)
        {
            return null;
        }

        Document? document = await _chatRepository.GetDocumentAsync(documentId.Value, userId, cancellationToken);

        if (document is null ||
            document.SubjectId != subjectId ||
            (chapterId.HasValue && document.ChapterId != chapterId.Value))
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(ChatAskDto.DocumentId), "Tài liệu không thuộc phạm vi đã chọn.")
            ]);
        }

        if (document.ProcessingStatus != DocumentProcessingStatus.Indexed.ToString() || document.ChunkCount == 0)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(ChatAskDto.DocumentId), "Tài liệu này chưa được xử lý nên chưa thể hỏi đáp.")
            ]);
        }

        return document;
    }

    private static string ValidateQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(ChatAskDto.Question), "Vui lòng nhập câu hỏi.")
            ]);
        }

        string normalizedQuestion = question.Trim();

        if (normalizedQuestion.Length < 2 || normalizedQuestion.Length > MaxQuestionLength)
        {
            throw new BusinessValidationException(
            [
                new ValidationError(nameof(ChatAskDto.Question), $"Câu hỏi phải từ 2 đến {MaxQuestionLength} ký tự.")
            ]);
        }

        return normalizedQuestion;
    }

    private static IReadOnlyList<RankedChunk> RankChunks(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyDictionary<long, double> vectorScores,
        IReadOnlyList<string> questionTerms)
    {
        return chunks
            .Select(chunk =>
            {
                double vectorScore = vectorScores.TryGetValue(chunk.DocumentChunkId, out double score) ? score : 0;
                double normalizedVectorScore = Math.Clamp((vectorScore + 1) / 2, 0, 1);
                double lexicalScore = CalculateLexicalScore(chunk, questionTerms);
                double finalScore = lexicalScore * 0.8 + normalizedVectorScore * 0.2;

                return new RankedChunk(chunk, vectorScore, normalizedVectorScore, lexicalScore, finalScore);
            })
            .OrderByDescending(chunk => chunk.FinalScore)
            .ThenBy(chunk => chunk.Chunk.DocumentId)
            .ThenBy(chunk => chunk.Chunk.ChunkIndex)
            .ToList();
    }

    private static double CalculateLexicalScore(DocumentChunk chunk, IReadOnlyList<string> questionTerms)
    {
        if (questionTerms.Count == 0)
        {
            return 0;
        }

        string normalizedContent = NormalizeForSearch(
            $"{chunk.Content} {chunk.SectionTitle} {chunk.Document.Title} {chunk.Document.OriginalFileName} {chunk.Document.Chapter?.Title}");
        int matchedTerms = questionTerms.Count(term => normalizedContent.Contains(term, StringComparison.OrdinalIgnoreCase));
        double score = (double)matchedTerms / questionTerms.Count;

        Match chapterMatch = Regex.Match(
            string.Join(' ', questionTerms),
            @"\bchapter\s+(\d+)\b|\bchuong\s+(\d+)\b",
            RegexOptions.IgnoreCase);

        if (chapterMatch.Success)
        {
            string chapterNumber = chapterMatch.Groups[1].Success
                ? chapterMatch.Groups[1].Value
                : chapterMatch.Groups[2].Value;

            if (Regex.IsMatch(
                    normalizedContent,
                    $@"\b(chapter|chuong)\s+{Regex.Escape(chapterNumber)}\b",
                    RegexOptions.IgnoreCase))
            {
                score += 1.2;
            }
        }

        if (normalizedContent.Contains("nhan vat chinh", StringComparison.OrdinalIgnoreCase) &&
            questionTerms.Contains("nhan", StringComparer.OrdinalIgnoreCase) &&
            questionTerms.Contains("vat", StringComparer.OrdinalIgnoreCase))
        {
            score += 0.8;
        }

        return score;
    }

    private static string[] ExtractSearchTerms(string question)
    {
        string normalizedQuestion = NormalizeForSearch(question);

        List<string> terms = normalizedQuestion
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => (term.Length >= 2 || term.All(char.IsDigit)) && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (IsToolQuestion(normalizedQuestion))
        {
            terms.AddRange(["godot", "unity", "engine", "tool", "framework", "gdscript", "csharp", "blender"]);
        }

        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] BuildKeywordSearchTerms(
        string question,
        string rewrittenQuery,
        IReadOnlyList<string> questionTerms,
        ChatQuestionIntent intent)
    {
        var keywords = new List<string>();
        string normalizedQuestion = NormalizeForSearch($"{question} {rewrittenQuery}");

        keywords.AddRange(questionTerms);

        Match chapterMatch = Regex.Match(
            normalizedQuestion,
            @"\b(chapter|chuong)\s+(?<number>\d+)\b",
            RegexOptions.IgnoreCase);

        if (chapterMatch.Success)
        {
            string chapterNumber = chapterMatch.Groups["number"].Value;
            keywords.Add($"Chapter {chapterNumber}");
            keywords.Add($"Chương {chapterNumber}");
            keywords.Add($"chapter {chapterNumber}");
            keywords.Add($"chuong {chapterNumber}");
        }

        if (intent == ChatQuestionIntent.Tool || IsToolQuestion(normalizedQuestion))
        {
            keywords.AddRange(["Godot", "Unity", "GDScript", "C#", "Blender", "engine", "framework", "tool", "công cụ", "phần mềm"]);
        }

        if (intent == ChatQuestionIntent.Character)
        {
            keywords.AddRange(["Nhân vật chính", "nhân vật", "main character", "protagonist"]);
        }

        return keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsToolQuestion(string normalizedQuestion)
    {
        string[] toolTerms =
        [
            "tool",
            "cong cu",
            "ngon ngu",
            "phan mem",
            "engine",
            "framework",
            "dung gi",
            "su dung"
        ];

        return toolTerms.Any(term => normalizedQuestion.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeForSearch(string value)
    {
        value = Regex.Replace(value, @"\bC#\b", "CSharp", RegexOptions.IgnoreCase);
        string withoutDiacritics = RemoveDiacritics(value).ToLowerInvariant();
        var builder = new StringBuilder(withoutDiacritics.Length);

        foreach (char character in withoutDiacritics)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString();
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char character in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }

    private static ChatDocumentOptionDto MapDocumentOption(Document document)
    {
        bool canChat = document.ProcessingStatus == DocumentProcessingStatus.Indexed.ToString() &&
                       document.ChunkCount > 0;

        return new ChatDocumentOptionDto(
            document.DocumentId,
            document.SubjectId,
            document.ChapterId,
            document.OriginalFileName,
            document.Title,
            document.ProcessingStatus,
            document.ChunkCount,
            canChat);
    }

    private static ChatConversationListItemDto MapConversation(ChatConversation conversation)
    {
        return new ChatConversationListItemDto(
            conversation.ChatConversationId,
            conversation.Title,
            conversation.SubjectId,
            conversation.ChapterId,
            conversation.DocumentId,
            conversation.UpdatedAt);
    }

    private static ChatMessageDto MapMessage(ChatMessage message)
    {
        return new ChatMessageDto(
            message.ChatMessageId,
            message.UserQuestion,
            message.AiAnswer,
            message.CreatedAt,
            message.ChatCitations
                .OrderByDescending(citation => citation.SimilarityScore)
                .Select(MapCitation)
                .ToList());
    }

    private static ChatCitationDto MapCitation(ChatCitation citation)
    {
        return new ChatCitationDto(
            citation.DocumentId,
            citation.DocumentChunkId,
            citation.DocumentName,
            citation.PageNumber,
            citation.Snippet,
            citation.SimilarityScore);
    }

    private static ChatCitationDto MapCitation(RankedChunk rankedChunk)
    {
        Document document = rankedChunk.Chunk.Document;

        return new ChatCitationDto(
            document.DocumentId,
            rankedChunk.Chunk.DocumentChunkId,
            document.OriginalFileName,
            rankedChunk.Chunk.PageNumber,
            BuildSnippet(rankedChunk.Chunk.Content),
            Math.Round(rankedChunk.FinalScore, 4));
    }

    private static string BuildSnippet(string content)
    {
        string normalizedContent = NormalizeWhitespace(content);

        return normalizedContent.Length <= MaxSnippetLength
            ? normalizedContent
            : normalizedContent[..MaxSnippetLength].Trim() + "...";
    }

    private static string NormalizeWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        bool previousWasWhiteSpace = false;

        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhiteSpace)
                {
                    builder.Append(' ');
                    previousWasWhiteSpace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhiteSpace = false;
        }

        return builder.ToString().Trim();
    }

    private static string BuildConversationTitle(string question, Subject subject, Chapter? chapter, Document? document)
    {
        string title = NormalizeWhitespace(question);

        if (title.Length > 0)
        {
            return title.Length <= 80 ? title : title[..80].Trim() + "...";
        }

        if (document is not null)
        {
            return document.OriginalFileName;
        }

        return chapter is null ? subject.Name : $"{subject.Name} / {chapter.Title}";
    }

    private sealed record RankedChunk(
        DocumentChunk Chunk,
        double VectorScore,
        double NormalizedVectorScore,
        double LexicalScore,
        double FinalScore);
}
