using System.Security.Claims;
using BusinessLogic.DTOs;
using BusinessLogic.Services;
using BusinessLogic.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;

namespace Presentation.Controllers;

[Authorize]
public sealed class SubjectController : Controller
{
    private readonly ISubjectService _subjectService;

    public SubjectController(ISubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = new SubjectIndexViewModel
        {
            Subjects = await _subjectService.GetSubjectsAsync(GetCurrentUserId(), cancellationToken)
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        SubjectDetailsDto? subject = await _subjectService.GetSubjectDetailsAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        return subject is null
            ? NotFound()
            : View(new SubjectDetailsViewModel { Subject = subject });
    }

    public IActionResult Create()
    {
        return View(new SubjectFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SubjectFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        try
        {
            int subjectId = await _subjectService.CreateSubjectAsync(
                new SubjectUpsertDto(null, GetCurrentUserId(), viewModel.Name, viewModel.Description),
                cancellationToken);

            TempData["SuccessMessage"] = "Đã tạo môn học.";

            return RedirectToAction(nameof(Details), new { id = subjectId });
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);
            return View(viewModel);
        }
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        SubjectDetailsDto? subject = await _subjectService.GetSubjectDetailsAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        return View(new SubjectFormViewModel
        {
            SubjectId = subject.SubjectId,
            Name = subject.Name,
            Description = subject.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        SubjectFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!viewModel.SubjectId.HasValue)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        try
        {
            bool updated = await _subjectService.UpdateSubjectAsync(
                new SubjectUpsertDto(
                    viewModel.SubjectId,
                    GetCurrentUserId(),
                    viewModel.Name,
                    viewModel.Description),
                cancellationToken);

            if (!updated)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = "Đã cập nhật môn học.";

            return RedirectToAction(nameof(Details), new { id = viewModel.SubjectId.Value });
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);
            return View(viewModel);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            bool deleted = await _subjectService.DeleteSubjectAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

            TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
                ? "Đã xóa môn học."
                : "Không tìm thấy môn học cần xóa.";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ErrorMessage"] = ex.Errors.FirstOrDefault()?.ErrorMessage
                ?? "Không thể xóa môn học.";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> CreateChapter(int subjectId, CancellationToken cancellationToken)
    {
        SubjectDetailsDto? subject = await _subjectService.GetSubjectDetailsAsync(
            subjectId,
            GetCurrentUserId(),
            cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        return View(new ChapterFormViewModel
        {
            SubjectId = subject.SubjectId,
            SubjectName = subject.Name
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateChapter(
        ChapterFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSubjectNameAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        try
        {
            await _subjectService.CreateChapterAsync(
                new ChapterUpsertDto(
                    null,
                    viewModel.SubjectId,
                    GetCurrentUserId(),
                    viewModel.Title,
                    viewModel.Description),
                cancellationToken);

            TempData["SuccessMessage"] = "Đã tạo chương.";

            return RedirectToAction(nameof(Details), new { id = viewModel.SubjectId });
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);
            await PopulateSubjectNameAsync(viewModel, cancellationToken);

            return View(viewModel);
        }
    }

    public async Task<IActionResult> EditChapter(int id, CancellationToken cancellationToken)
    {
        ChapterListItemDto? chapter = await _subjectService.GetChapterAsync(
            id,
            GetCurrentUserId(),
            cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        SubjectDetailsDto? subject = await _subjectService.GetSubjectDetailsAsync(
            chapter.SubjectId,
            GetCurrentUserId(),
            cancellationToken);

        return View(new ChapterFormViewModel
        {
            ChapterId = chapter.ChapterId,
            SubjectId = chapter.SubjectId,
            SubjectName = subject?.Name,
            Title = chapter.Title,
            Description = chapter.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditChapter(
        ChapterFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!viewModel.ChapterId.HasValue)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateSubjectNameAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        try
        {
            bool updated = await _subjectService.UpdateChapterAsync(
                new ChapterUpsertDto(
                    viewModel.ChapterId,
                    viewModel.SubjectId,
                    GetCurrentUserId(),
                    viewModel.Title,
                    viewModel.Description),
                cancellationToken);

            if (!updated)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = "Đã cập nhật chương.";

            return RedirectToAction(nameof(Details), new { id = viewModel.SubjectId });
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);
            await PopulateSubjectNameAsync(viewModel, cancellationToken);

            return View(viewModel);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChapter(int id, int subjectId, CancellationToken cancellationToken)
    {
        try
        {
            bool deleted = await _subjectService.DeleteChapterAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

            TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
                ? "Đã xóa chương."
                : "Không tìm thấy chương cần xóa.";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ErrorMessage"] = ex.Errors.FirstOrDefault()?.ErrorMessage
                ?? "Không thể xóa chương.";
        }

        return RedirectToAction(nameof(Details), new { id = subjectId });
    }

    private async Task PopulateSubjectNameAsync(
        ChapterFormViewModel viewModel,
        CancellationToken cancellationToken)
    {
        SubjectDetailsDto? subject = await _subjectService.GetSubjectDetailsAsync(
            viewModel.SubjectId,
            GetCurrentUserId(),
            cancellationToken);

        viewModel.SubjectName = subject?.Name;
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

    private void AddValidationErrors(BusinessValidationException exception)
    {
        foreach (ValidationError error in exception.Errors)
        {
            ModelState.AddModelError(error.FieldName, error.ErrorMessage);
        }
    }
}
