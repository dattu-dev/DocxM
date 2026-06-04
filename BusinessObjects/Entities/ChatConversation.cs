namespace BusinessObjects.Entities;

public sealed class ChatConversation
{
    public int ChatConversationId { get; set; }

    public int UserId { get; set; }

    // Scope lưu lại phạm vi chat để không reuse conversation sai quyền.
    public int? SubjectId { get; set; }

    public int? ChapterId { get; set; }

    public int? DocumentId { get; set; }

    public string Title { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public AppUser User { get; set; } = null!;

    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
