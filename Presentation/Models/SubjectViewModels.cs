using System.ComponentModel.DataAnnotations;
using BusinessLogic.DTOs;

namespace Presentation.Models;

public sealed class SubjectIndexViewModel
{
    public IReadOnlyList<SubjectListItemDto> Subjects { get; set; } = Array.Empty<SubjectListItemDto>();
}

public sealed class SubjectDetailsViewModel
{
    public SubjectDetailsDto Subject { get; set; } = null!;
}

public sealed class SubjectFormViewModel
{
    public int? SubjectId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Tên môn học phải từ 2 đến 200 ký tự.")]
    [Display(Name = "Tên môn học")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }
}
