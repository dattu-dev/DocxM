using BusinessLogic.DTOs;
using BusinessLogic.Services;
using BusinessLogic.Validation;
using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Presentation.Models;
using Presentation.Services;

namespace Presentation.Controllers;

[Authorize]
public sealed class ChatController : Controller
{
    private const string CurrentConversationSessionKey = "Chat.CurrentConversationId";

    private readonly IChatService _chatService;
    private readonly IWebHostEnvironment _environment;
    private readonly ICurrentUserService _currentUser;

    public ChatController(
        IChatService chatService,
        IWebHostEnvironment environment,
        ICurrentUserService currentUser)
    {
        _chatService = chatService;
        _environment = environment;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(
        int? subjectId,
        int? chapterId,
        int? documentId,
        int? conversationId,
        CancellationToken cancellationToken)
    {
        bool hasScopeQuery = subjectId.HasValue || chapterId.HasValue || documentId.HasValue;
        int? currentConversationId = NormalizeId(conversationId);

        if (currentConversationId.HasValue)
        {
            HttpContext.Session.SetInt32(CurrentConversationSessionKey, currentConversationId.Value);
        }
        else if (hasScopeQuery)
        {
            HttpContext.Session.Remove(CurrentConversationSessionKey);
        }
        else
        {
            currentConversationId = HttpContext.Session.GetInt32(CurrentConversationSessionKey);
        }

        ChatIndexViewModel viewModel = await BuildViewModelAsync(
            new ChatScopeDto(
                NormalizeId(subjectId),
                NormalizeId(chapterId),
                NormalizeId(documentId),
                currentConversationId,
                // Scope gửi xuống service phải mang đúng ngữ cảnh role hiện tại.
                GetOwnerFilterUserId(),
                GetViewerFilterUserId()),
            cancellationToken);

        if (viewModel.ConversationId.HasValue)
        {
            HttpContext.Session.SetInt32(CurrentConversationSessionKey, viewModel.ConversationId.Value);
        }
        else
        {
            HttpContext.Session.Remove(CurrentConversationSessionKey);
        }

        return View(viewModel);
    }

    public IActionResult New()
    {
        HttpContext.Session.Remove(CurrentConversationSessionKey);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConversation(
        int conversationId,
        CancellationToken cancellationToken)
    {
        int? currentConversationId = HttpContext.Session.GetInt32(CurrentConversationSessionKey);

        bool deleted = await _chatService.DeleteConversationAsync(
            conversationId,
            _currentUser.UserId,
            cancellationToken);

        if (deleted && currentConversationId == conversationId)
        {
            HttpContext.Session.Remove(CurrentConversationSessionKey);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(
        ChatIndexViewModel viewModel,
        CancellationToken cancellationToken)
    {
        // Action này nhận câu hỏi từ UI và chuyển sang ChatService xử lý RAG.
        viewModel.SubjectId = NormalizeId(viewModel.SubjectId);
        viewModel.ChapterId = NormalizeId(viewModel.ChapterId);
        viewModel.DocumentId = NormalizeId(viewModel.DocumentId);
        viewModel.ConversationId = NormalizeId(viewModel.ConversationId);

        if (!ModelState.IsValid)
        {
            await PopulateViewModelAsync(viewModel, cancellationToken);
            return View(nameof(Index), viewModel);
        }

        try
        {
            ChatAnswerDto answer = await _chatService.AskAsync(
                new ChatAskDto(
                    _currentUser.UserId,
                    viewModel.ConversationId,
                    viewModel.SubjectId,
                    viewModel.ChapterId,
                    viewModel.DocumentId,
                    viewModel.Question,
                    GetWebRootPath(),
                    // Service/repository dùng hai filter này để chặn chat ngoài phạm vi quyền.
                    GetOwnerFilterUserId(),
                    GetViewerFilterUserId()),
                cancellationToken);

            HttpContext.Session.SetInt32(CurrentConversationSessionKey, answer.ConversationId);

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);
            await PopulateViewModelAsync(viewModel, cancellationToken);

            return View(nameof(Index), viewModel);
        }
    }

    private async Task<ChatIndexViewModel> BuildViewModelAsync(
        ChatScopeDto scope,
        CancellationToken cancellationToken)
    {
        var viewModel = new ChatIndexViewModel
        {
            SubjectId = scope.SubjectId,
            ChapterId = scope.ChapterId,
            DocumentId = scope.DocumentId,
            ConversationId = scope.ConversationId
        };

        await PopulateViewModelAsync(viewModel, cancellationToken);

        return viewModel;
    }

    private async Task PopulateViewModelAsync(
        ChatIndexViewModel viewModel,
        CancellationToken cancellationToken)
    {
        ChatPageDto page = await _chatService.GetChatPageAsync(
            _currentUser.UserId,
            new ChatScopeDto(
                viewModel.SubjectId,
                viewModel.ChapterId,
                viewModel.DocumentId,
                viewModel.ConversationId,
                GetOwnerFilterUserId(),
                GetViewerFilterUserId()),
            cancellationToken);

        HashSet<int> validSubjectIds = page.Subjects.Select(subject => subject.SubjectId).ToHashSet();
        HashSet<int> validChapterIds = page.Chapters.Select(chapter => chapter.ChapterId).ToHashSet();
        HashSet<int> validDocumentIds = page.Documents.Select(document => document.DocumentId).ToHashSet();

        if (!viewModel.SubjectId.HasValue || !validSubjectIds.Contains(viewModel.SubjectId.Value))
        {
            viewModel.SubjectId = null;
        }

        if (!viewModel.ChapterId.HasValue || !validChapterIds.Contains(viewModel.ChapterId.Value))
        {
            viewModel.ChapterId = null;
        }

        if (!viewModel.DocumentId.HasValue || !validDocumentIds.Contains(viewModel.DocumentId.Value))
        {
            viewModel.DocumentId = null;
        }

        viewModel.HasIndexedDocuments = page.HasIndexedDocuments;
        viewModel.HasChatReadyDocuments = page.Documents.Any(document => document.CanChat);
        viewModel.Messages = page.Messages;
        viewModel.Conversations = page.Conversations;
        viewModel.ConversationId = page.CurrentConversationId;
        viewModel.SubjectId = page.CurrentSubjectId;
        viewModel.ChapterId = page.CurrentChapterId;
        viewModel.DocumentId = page.CurrentDocumentId;
        viewModel.Subjects = BuildSubjectSelectList(page.Subjects, viewModel.SubjectId);
        viewModel.Chapters = BuildChapterSelectList(page.Chapters, viewModel.SubjectId, viewModel.ChapterId);
        viewModel.Documents = BuildDocumentSelectList(page.Documents, viewModel.SubjectId, viewModel.DocumentId);
    }

    private static List<SelectListItem> BuildSubjectSelectList(
        IReadOnlyList<SubjectOptionDto> subjects,
        int? selectedSubjectId)
    {
        var items = new List<SelectListItem>
        {
            new("-- Chọn môn học --", string.Empty)
        };

        items.AddRange(subjects.Select(subject => new SelectListItem(
            subject.Name,
            subject.SubjectId.ToString(),
            subject.SubjectId == selectedSubjectId)));

        return items;
    }

    private static List<SelectListItem> BuildChapterSelectList(
        IReadOnlyList<ChapterOptionDto> chapters,
        int? selectedSubjectId,
        int? selectedChapterId)
    {
        var items = new List<SelectListItem>
        {
            new(selectedSubjectId.HasValue ? "-- Tất cả chương --" : "-- Chọn môn học trước --", string.Empty)
        };

        items.AddRange(chapters.Select(chapter => new SelectListItem(
            chapter.Title,
            chapter.ChapterId.ToString(),
            chapter.ChapterId == selectedChapterId)));

        return items;
    }

    private static List<SelectListItem> BuildDocumentSelectList(
        IReadOnlyList<ChatDocumentOptionDto> documents,
        int? selectedSubjectId,
        int? selectedDocumentId)
    {
        var items = new List<SelectListItem>
        {
            new(selectedSubjectId.HasValue ? "-- Tất cả tài liệu --" : "-- Chọn môn học trước --", string.Empty)
        };

        items.AddRange(documents.Select(document => new SelectListItem(
            BuildDocumentOptionText(document),
            document.DocumentId.ToString(),
            document.DocumentId == selectedDocumentId)
        {
            Disabled = !document.CanChat
        }));

        return items;
    }

    private static string BuildDocumentOptionText(ChatDocumentOptionDto document)
    {
        if (document.CanChat)
        {
            return $"{document.OriginalFileName} - {document.Title}";
        }

        string status = document.ProcessingStatus switch
        {
            "Pending" or "Processing" => "Đang xử lý",
            "Ready" or "Indexed" or "Completed" => "Sẵn sàng",
            "Failed" or "Error" => "Lỗi xử lý",
            "NotIndexed" or "Uploaded" => "Chưa xử lý",
            _ => document.ProcessingStatus
        };

        return $"{document.OriginalFileName} - {document.Title} ({status})";
    }

    private string GetWebRootPath()
    {
        return string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
    }

    private int? GetOwnerFilterUserId()
    {
        // Instructor chat với dữ liệu do mình sở hữu/upload.
        return _currentUser.IsInstructor ? _currentUser.UserId : null;
    }

    private int? GetViewerFilterUserId()
    {
        // Student chat qua SubjectPermissions, không theo dữ liệu của Instructor.
        return _currentUser.IsStudent ? _currentUser.UserId : null;
    }

    private void AddValidationErrors(BusinessValidationException exception)
    {
        foreach (ValidationError error in exception.Errors)
        {
            ModelState.AddModelError(error.FieldName, error.ErrorMessage);
        }
    }

    private static int? NormalizeId(int? id)
    {
        return id is > 0 ? id : null;
    }
}
