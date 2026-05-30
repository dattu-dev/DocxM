using System.ComponentModel.DataAnnotations;
using BusinessLogic.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Presentation.Models;

public sealed class ChatIndexViewModel
{
    public int? ConversationId { get; set; }

    [Display(Name = "Môn học")]
    public int? SubjectId { get; set; }

    [Display(Name = "Chương")]
    public int? ChapterId { get; set; }

    [Display(Name = "Tài liệu")]
    public int? DocumentId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập câu hỏi.")]
    [StringLength(1000, MinimumLength = 2, ErrorMessage = "Câu hỏi phải từ 2 đến 1000 ký tự.")]
    [Display(Name = "Câu hỏi")]
    public string Question { get; set; } = string.Empty;

    public bool HasIndexedDocuments { get; set; }

    public bool HasChatReadyDocuments { get; set; }

    public List<SelectListItem> Subjects { get; set; } = new();

    public List<SelectListItem> Chapters { get; set; } = new();

    public List<SelectListItem> Documents { get; set; } = new();

    public IReadOnlyList<ChatConversationListItemDto> Conversations { get; set; } = Array.Empty<ChatConversationListItemDto>();

    public IReadOnlyList<ChatMessageDto> Messages { get; set; } = Array.Empty<ChatMessageDto>();
}
