using BusinessLogic.DTOs;
using BusinessLogic.Services;
using BusinessLogic.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Presentation.Models;
using System.Security.Claims;

namespace Presentation.Controllers;

[Authorize]
public sealed class DocumentController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly IWebHostEnvironment _environment;

    public DocumentController(
        IDocumentService documentService,
        IWebHostEnvironment environment)
    {
        _documentService = documentService;
        _environment = environment;
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

        var filter = new DocumentFilterDto(subjectId, chapterId, searchTerm, GetCurrentUserId());

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

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var viewModel = new DocumentUploadViewModel();
        await PopulateSelectListsAsync(viewModel, null, null, cancellationToken);

        return View(viewModel);
    }

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
            await PopulateSelectListsAsync(viewModel, viewModel.SubjectId, viewModel.ChapterId, cancellationToken);
            return View(viewModel);
        }

        try
        {
            await using Stream stream = viewModel.File.OpenReadStream();

            DocumentUploadResultDto result = await _documentService.UploadDocumentAsync(
                new DocumentUploadDto
                {
                    UploadedByUserId = GetCurrentUserId(),
                    SubjectId = viewModel.SubjectId,
                    ChapterId = viewModel.ChapterId,
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
                $"Đã tải tài liệu lên. Trạng thái: {GetProcessingStatusLabel(result.ProcessingStatus)}, số đoạn: {result.ChunkCount}.";

            return RedirectToAction(nameof(Details), new { id = result.DocumentId });
        }
        catch (BusinessValidationException ex)
        {
            foreach (ValidationError error in ex.Errors)
            {
                ModelState.AddModelError(error.FieldName, error.ErrorMessage);
            }

            await PopulateSelectListsAsync(viewModel, viewModel.SubjectId, viewModel.ChapterId, cancellationToken);

            return View(viewModel);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateSelectListsAsync(viewModel, viewModel.SubjectId, viewModel.ChapterId, cancellationToken);

            return View(viewModel);
        }
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        DocumentDetailsDto? document = await _documentService.GetDocumentDetailsAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return View(new DocumentDetailsViewModel { Document = document });
    }

    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        DocumentFileDto? file = await _documentService.GetDocumentFileAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        if (file is null)
        {
            return NotFound();
        }

        string physicalPath = GetSafePhysicalPath(file.StoragePath);

        if (!System.IO.File.Exists(physicalPath))
        {
            return NotFound();
        }

        return PhysicalFile(physicalPath, file.ContentType, file.OriginalFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        bool deleted = await _documentService.DeleteDocumentAsync(
            id,
            GetCurrentUserId(),
            GetWebRootPath(),
            cancellationToken);

        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
            ? "Đã xóa tài liệu."
            : "Không tìm thấy tài liệu cần xóa.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReIndex(int id, CancellationToken cancellationToken)
    {
        try
        {
            DocumentUploadResultDto? result = await _documentService.ReIndexDocumentAsync(
                id,
                GetCurrentUserId(),
                GetWebRootPath(),
                cancellationToken);

            TempData[result is null ? "ErrorMessage" : "SuccessMessage"] = result is null
                ? "Không tìm thấy tài liệu cần lập chỉ mục lại."
                : $"Đã lập chỉ mục lại tài liệu. Trạng thái: {GetProcessingStatusLabel(result.ProcessingStatus)}, số đoạn: {result.ChunkCount}.";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ErrorMessage"] = ex.Errors.FirstOrDefault()?.ErrorMessage
                ?? "Không thể lập chỉ mục lại tài liệu.";
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
        int? selectedChapterId,
        CancellationToken cancellationToken)
    {
        viewModel.Subjects = await BuildSubjectSelectListAsync(selectedSubjectId, cancellationToken);
        viewModel.Chapters = await BuildChapterSelectListAsync(selectedSubjectId, selectedChapterId, cancellationToken);
    }

    private async Task<List<SelectListItem>> BuildSubjectSelectListAsync(
        int? selectedSubjectId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectOptionDto> subjects = await _documentService.GetSubjectsAsync(
            GetCurrentUserId(),
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
        IReadOnlyList<ChapterOptionDto> chapters = await _documentService.GetChaptersAsync(
            selectedSubjectId,
            GetCurrentUserId(),
            cancellationToken);

        var items = new List<SelectListItem>
        {
            new("-- Chọn chương --", string.Empty)
        };

        items.AddRange(chapters.Select(chapter => new SelectListItem(
            $"Chương {chapter.ChapterNumber}: {chapter.Title}",
            chapter.ChapterId.ToString(),
            chapter.ChapterId == selectedChapterId)));

        return items;
    }

    private string GetWebRootPath()
    {
        return string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
    }

    private string GetSafePhysicalPath(string relativePath)
    {
        string root = Path.GetFullPath(GetWebRootPath());
        string fullPath = Path.GetFullPath(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return fullPath;
    }

    private int GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out int parsedUserId))
        {
            throw new InvalidOperationException("Không xác định được người dùng hiện tại.");
        }

        return parsedUserId;
    }

    private static string? NormalizeSearchTerm(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        return searchTerm.Trim();
    }

    private static string GetProcessingStatusLabel(string status)
    {
        return status switch
        {
            "Indexed" => "Đã lập chỉ mục",
            "Failed" => "Lỗi xử lý",
            "Uploaded" => "Đã tải lên",
            _ => status
        };
    }
}
