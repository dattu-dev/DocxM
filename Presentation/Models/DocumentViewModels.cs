using System.ComponentModel.DataAnnotations;
using BusinessLogic.DTOs;
using BusinessLogic.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Presentation.Validation;

namespace Presentation.Models;

public sealed class DocumentIndexViewModel
{
    public int? SubjectId { get; set; }

    public int? ChapterId { get; set; }

    [StringLength(100)]
    public string? SearchTerm { get; set; }

    public IReadOnlyList<DocumentListItemDto> Documents { get; set; } = Array.Empty<DocumentListItemDto>();

    public List<SelectListItem> Subjects { get; set; } = new();

    public List<SelectListItem> Chapters { get; set; } = new();
}

public sealed class DocumentUploadViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    [Display(Name = "Môn học")]
    public int SubjectId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chương.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn chương.")]
    [Display(Name = "Chương")]
    public int? ChapterId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    [StringLength(DocumentUploadRules.MaxTitleLength, MinimumLength = 2, ErrorMessage = "Tiêu đề phải từ 2 đến 250 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [StringLength(DocumentUploadRules.MaxDescriptionLength, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn file.")]
    [DocumentFile]
    [Display(Name = "Tài liệu")]
    public IFormFile? File { get; set; }

    public List<SelectListItem> Subjects { get; set; } = new();

    public List<SelectListItem> Chapters { get; set; } = new();
}

public sealed class DocumentDetailsViewModel
{
    public DocumentDetailsDto Document { get; set; } = null!;
}
