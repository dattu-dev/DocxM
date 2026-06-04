using BusinessLogic.DTOs;
using BusinessLogic.Services;
using BusinessLogic.Validation;
using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Presentation.Services;

namespace Presentation.Controllers;

[Authorize]
public sealed class SubjectController : Controller
{
    private readonly ICurrentUserService _currentUser;
    private readonly ISubjectService _subjectService;

    public SubjectController(
        ISubjectService subjectService,
        ICurrentUserService currentUser)
    {
        _subjectService = subjectService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = new SubjectIndexViewModel
        {
            // Instructor xem môn mình tạo, Student chỉ xem môn đã được cấp quyền.
            Subjects = await _subjectService.GetSubjectsAsync(
                GetOwnerFilterUserId(),
                GetViewerFilterUserId(),
                cancellationToken)
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        SubjectDetailsDto? subject = await _subjectService.GetSubjectDetailsAsync(
            id,
            GetOwnerFilterUserId(),
            GetViewerFilterUserId(),
            cancellationToken);

        return subject is null
            ? NotFound()
            : View(new SubjectDetailsViewModel { Subject = subject });
    }

    [Authorize(Roles = UserRoles.Instructor)]
    public async Task<IActionResult> Permissions(int id, CancellationToken cancellationToken)
    {
        SubjectPermissionsViewModel? viewModel = await BuildSubjectPermissionsViewModelAsync(
            id,
            string.Empty,
            cancellationToken);

        return viewModel is null
            ? NotFound()
            : View(viewModel);
    }

    [Authorize(Roles = UserRoles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantPermission(
        SubjectPermissionsViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Không thể cấp quyền. Vui lòng kiểm tra email sinh viên.";

            SubjectPermissionsViewModel? currentViewModel = await BuildSubjectPermissionsViewModelAsync(
                viewModel.SubjectId,
                viewModel.StudentEmail,
                cancellationToken);

            return currentViewModel is null
                ? NotFound()
                : View(nameof(Permissions), currentViewModel);
        }

        try
        {
            // Chỉ Instructor sở hữu Subject mới được cấp quyền cho Student.
            bool granted = await _subjectService.GrantSubjectPermissionAsync(
                new SubjectPermissionGrantDto(
                    viewModel.SubjectId,
                    _currentUser.UserId,
                    viewModel.StudentEmail),
                cancellationToken);

            if (!granted)
            {
                TempData["ErrorMessage"] = "Không thể cấp quyền cho môn học này.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Đã cấp quyền xem môn học cho sinh viên.";

            return RedirectToAction(nameof(Permissions), new { id = viewModel.SubjectId });
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);
            TempData["ErrorMessage"] = ex.Errors.FirstOrDefault()?.ErrorMessage
                ?? "Không thể cấp quyền xem môn học.";

            SubjectPermissionsViewModel? currentViewModel = await BuildSubjectPermissionsViewModelAsync(
                viewModel.SubjectId,
                viewModel.StudentEmail,
                cancellationToken);

            return currentViewModel is null
                ? NotFound()
                : View(nameof(Permissions), currentViewModel);
        }
    }

    [Authorize(Roles = UserRoles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokePermission(
        int subjectId,
        int studentUserId,
        CancellationToken cancellationToken)
    {
        bool revoked = await _subjectService.RevokeSubjectPermissionAsync(
            subjectId,
            _currentUser.UserId,
            studentUserId,
            cancellationToken);

        TempData[revoked ? "SuccessMessage" : "ErrorMessage"] = revoked
            ? "Đã thu hồi quyền xem môn học."
            : "Không tìm thấy quyền cần thu hồi.";

        if (!revoked)
        {
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Permissions), new { id = subjectId });
    }

    [Authorize(Roles = UserRoles.Instructor)]
    public IActionResult Create()
    {
        return View(new SubjectFormViewModel());
    }

    [Authorize(Roles = UserRoles.Instructor)]
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
                new SubjectUpsertDto(null, _currentUser.UserId, viewModel.Name, viewModel.Description),
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

    [Authorize(Roles = UserRoles.Instructor)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        SubjectDetailsDto? subject = await _subjectService.GetSubjectDetailsAsync(
            id,
            _currentUser.UserId,
            null,
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

    [Authorize(Roles = UserRoles.Instructor)]
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
                    _currentUser.UserId,
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

    [Authorize(Roles = UserRoles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            bool deleted = await _subjectService.DeleteSubjectAsync(
                id,
                _currentUser.UserId,
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

    [Authorize(Roles = UserRoles.Instructor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChapter(int subjectId, int chapterId, CancellationToken cancellationToken)
    {
        try
        {
            bool deleted = await _subjectService.DeleteChapterAsync(
                subjectId,
                chapterId,
                _currentUser.UserId,
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

    private int? GetOwnerFilterUserId()
    {
        // Owner filter dùng cho Instructor để tránh truy cập Subject của giảng viên khác.
        return _currentUser.IsInstructor ? _currentUser.UserId : null;
    }

    private int? GetViewerFilterUserId()
    {
        // Viewer filter dùng cho Student để lọc theo SubjectPermissions.
        return _currentUser.IsStudent ? _currentUser.UserId : null;
    }

    private async Task<SubjectPermissionsViewModel?> BuildSubjectPermissionsViewModelAsync(
        int subjectId,
        string studentEmail,
        CancellationToken cancellationToken)
    {
        SubjectPermissionsDto? permissions = await _subjectService.GetSubjectPermissionsAsync(
            subjectId,
            _currentUser.UserId,
            cancellationToken);

        if (permissions is null)
        {
            return null;
        }

        return new SubjectPermissionsViewModel
        {
            SubjectId = permissions.SubjectId,
            SubjectName = permissions.SubjectName,
            StudentEmail = studentEmail,
            Students = permissions.Students
        };
    }

    private void AddValidationErrors(BusinessValidationException exception)
    {
        foreach (ValidationError error in exception.Errors)
        {
            ModelState.AddModelError(error.FieldName, error.ErrorMessage);
        }
    }
}
