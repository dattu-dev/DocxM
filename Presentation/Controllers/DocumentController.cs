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
public sealed class DocumentController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IWebHostEnvironment _environment;
    private readonly ICurrentUserService _currentUser;

    public DocumentController(
        IDocumentService documentService,
        IFileStorageService fileStorageService,
        IWebHostEnvironment environment,
        ICurrentUserService currentUser)
    {
        _documentService = documentService;
        _fileStorageService = fileStorageService;
        _environment = environment;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(
        int? subjectId,
        int? chapterId,
        string? searchTerm,
        CancellationToken cancellationToken)
    {
        subjectId = subjectId is > 0 ? subjectId : null;
        chapterId = chapterId is > 0 ? chapterId : null;
        searchTerm = NormalizeSearchTerm(searchTerm);

        if (searchTerm?.Length > 100)
        {
            ModelState.AddModelError(nameof(DocumentIndexViewModel.SearchTerm), "Từ khóa tìm kiếm tối đa 100 ký tự.");
            searchTerm = searchTerm[..100];
        }

        chapterId = await NormalizeChapterFilterAsync(subjectId, chapterId, cancellationToken);

        var filter = new DocumentFilterDto(
            subjectId,
            chapterId,
            searchTerm,
            GetOwnerFilterUserId(),
            GetViewerFilterUserId());

        var viewModel = new DocumentIndexViewModel
        {
            SubjectId = subjectId,
            ChapterId = chapterId,
            SearchTerm = searchTerm,
            Documents = await _documentService.GetDocumentsAsync(filter, cancellationToken)
        };

        await PopulateSelectListsAsync(viewModel, subjectId, chapterId, cancellationToken);

        return View(viewModel);
    }

    [Authorize(Roles = UserRoles.Instructor)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var viewModel = new DocumentUploadViewModel();
        await PopulateSelectListsAsync(viewModel, null, cancellationToken);

        return View(viewModel);
    }

    [Authorize(Roles = UserRoles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(DocumentUploadRules.MaxFileSizeBytes)]
    public async Task<IActionResult> Create(
        DocumentUploadViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (viewModel.File is { Length: > DocumentUploadRules.MaxFileSizeBytes })
        {
            ModelState.AddModelError(nameof(viewModel.File), "File tối đa là 100 MB.");
        }

        if (!ModelState.IsValid || viewModel.File is null)
        {
            await PopulateSelectListsAsync(viewModel, viewModel.SubjectId, cancellationToken);
            return View(viewModel);
        }

        try
        {
            await using Stream stream = viewModel.File.OpenReadStream();

            // Upload luôn gắn với Instructor hiện tại để service kiểm tra quyền sở hữu Subject.
            DocumentUploadResultDto result = await _documentService.UploadDocumentAsync(
                new DocumentUploadDto
                {
                    UploadedByUserId = _currentUser.UserId,
                    SubjectId = viewModel.SubjectId,
                    ChapterTitle = viewModel.ChapterTitle,
                    Title = viewModel.Title,
                    Description = viewModel.Description,
                    OriginalFileName = viewModel.File.FileName,
                    ContentType = viewModel.File.ContentType,
                    FileSizeBytes = viewModel.File.Length,
                    FileStream = stream,
                    WebRootPath = GetWebRootPath()
                },
                cancellationToken);

            TempData["SuccessMessage"] =
                $"Đã tải tài liệu lên. Trạng thái: {GetProcessingStatusLabel(result.ProcessingStatus)}.";

            return RedirectToAction(nameof(Details), new { id = result.DocumentId });
        }
        catch (BusinessValidationException ex)
        {
            foreach (ValidationError error in ex.Errors)
            {
                ModelState.AddModelError(error.FieldName, error.ErrorMessage);
            }

            await PopulateSelectListsAsync(viewModel, viewModel.SubjectId, cancellationToken);

            return View(viewModel);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateSelectListsAsync(viewModel, viewModel.SubjectId, cancellationToken);

            return View(viewModel);
        }
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        DocumentDetailsDto? document = await _documentService.GetDocumentDetailsAsync(
            id,
            GetOwnerFilterUserId(),
            GetViewerFilterUserId(),
            cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return View(new DocumentDetailsViewModel { Document = document });
    }

    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        // Download dùng chung service kiểm quyền với preview để không expose file path thật.
        DocumentFileDto? file = await _documentService.GetDocumentFileAsync(
            id,
            GetOwnerFilterUserId(),
            GetViewerFilterUserId(),
            cancellationToken);

        if (file is null)
        {
            return NotFound();
        }

        string physicalPath = _fileStorageService.GetSafePhysicalPath(GetWebRootPath(), file.StoragePath);

        if (!_fileStorageService.FileExists(physicalPath))
        {
            return NotFound();
        }

        return PhysicalFile(physicalPath, file.ContentType, file.OriginalFileName);
    }

    public async Task<IActionResult> Preview(int id, CancellationToken cancellationToken)
    {
        // Preview hiện chỉ mở PDF sau khi document đã qua kiểm tra quyền.
        DocumentFileDto? file = await _documentService.GetDocumentFileAsync(
            id,
            GetOwnerFilterUserId(),
            GetViewerFilterUserId(),
            cancellationToken);

        if (file is null)
        {
            return NotFound();
        }

        if (!IsPdf(file.OriginalFileName))
        {
            TempData["ErrorMessage"] = "Định dạng này chưa hỗ trợ xem trước, vui lòng tải file về.";
            return RedirectToAction(nameof(Details), new { id });
        }

        string physicalPath = _fileStorageService.GetSafePhysicalPath(GetWebRootPath(), file.StoragePath);

        if (!_fileStorageService.FileExists(physicalPath))
        {
            return NotFound();
        }

        return new PhysicalFileResult(physicalPath, "application/pdf")
        {
            EnableRangeProcessing = true
        };
    }

    [Authorize(Roles = UserRoles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        bool deleted = await _documentService.DeleteDocumentAsync(
            id,
            _currentUser.UserId,
            GetWebRootPath(),
            cancellationToken);

        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
            ? "Đã xóa tài liệu."
            : "Không tìm thấy tài liệu cần xóa.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = UserRoles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReIndex(int id, CancellationToken cancellationToken)
    {
        try
        {
            DocumentUploadResultDto? result = await _documentService.ReIndexDocumentAsync(
                id,
                _currentUser.UserId,
                GetWebRootPath(),
                cancellationToken);

            TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
                ? "Không tìm thấy tài liệu cần xử lý lại."
                : $"Đã xử lý lại tài liệu. Trạng thái: {GetProcessingStatusLabel(result.ProcessingStatus)}.";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ErrorMessage"] = ex.Errors.FirstOrDefault()?.ErrorMessage
                ?? "Không thể xử lý lại tài liệu.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateSelectListsAsync(
        DocumentIndexViewModel viewModel,
        int? selectedSubjectId,
        int? selectedChapterId,
        CancellationToken cancellationToken)
    {
        viewModel.Subjects = await BuildSubjectSelectListAsync(selectedSubjectId, cancellationToken);
        viewModel.Chapters = await BuildChapterSelectListAsync(selectedSubjectId, selectedChapterId, cancellationToken);
    }

    private async Task PopulateSelectListsAsync(
        DocumentUploadViewModel viewModel,
        int? selectedSubjectId,
        CancellationToken cancellationToken)
    {
        viewModel.Subjects = await BuildSubjectSelectListAsync(selectedSubjectId, cancellationToken);
    }

    private async Task<List<SelectListItem>> BuildSubjectSelectListAsync(
        int? selectedSubjectId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectOptionDto> subjects = await _documentService.GetSubjectsAsync(
            GetOwnerFilterUserId(),
            GetViewerFilterUserId(),
            cancellationToken);

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

    private async Task<List<SelectListItem>> BuildChapterSelectListAsync(
        int? selectedSubjectId,
        int? selectedChapterId,
        CancellationToken cancellationToken)
    {
        var items = new List<SelectListItem>
        {
            new(selectedSubjectId.HasValue ? "-- Chọn chương --" : "-- Chọn môn học trước --", string.Empty)
        };

        if (!selectedSubjectId.HasValue)
        {
            return items;
        }

        IReadOnlyList<ChapterOptionDto> chapters = await _documentService.GetChaptersAsync(
            selectedSubjectId,
            GetOwnerFilterUserId(),
            GetViewerFilterUserId(),
            cancellationToken);

        items.AddRange(chapters.Select(chapter => new SelectListItem(
            chapter.Title,
            chapter.ChapterId.ToString(),
            chapter.ChapterId == selectedChapterId)));

        return items;
    }

    private async Task<int?> NormalizeChapterFilterAsync(
        int? subjectId,
        int? chapterId,
        CancellationToken cancellationToken)
    {
        if (!subjectId.HasValue || !chapterId.HasValue)
        {
            return null;
        }

        IReadOnlyList<ChapterOptionDto> subjectChapters = await _documentService.GetChaptersAsync(
            subjectId,
            GetOwnerFilterUserId(),
            GetViewerFilterUserId(),
            cancellationToken);

        return subjectChapters.Any(chapter => chapter.ChapterId == chapterId.Value)
            ? chapterId
            : null;
    }

    private string GetWebRootPath()
    {
        return string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
    }

    private int? GetOwnerFilterUserId()
    {
        // Instructor chỉ lọc document do mình upload.
        return _currentUser.IsInstructor ? _currentUser.UserId : null;
    }

    private int? GetViewerFilterUserId()
    {
        // Student chỉ lọc document thuộc Subject được cấp quyền.
        return _currentUser.IsStudent ? _currentUser.UserId : null;
    }

    private static string? NormalizeSearchTerm(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        return searchTerm.Trim();
    }

    private static bool IsPdf(string fileName)
    {
        return string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetProcessingStatusLabel(string status)
    {
        return status switch
        {
            "Pending" or "Processing" => "Đang xử lý",
            "Ready" or "Indexed" or "Completed" => "Sẵn sàng",
            "Failed" or "Error" => "Lỗi xử lý",
            "NotIndexed" or "Uploaded" => "Chưa xử lý",
            _ => status
        };
    }
}
