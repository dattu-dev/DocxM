namespace BusinessObjects.Entities;

public sealed class ChatMessage
{
    public long ChatMessageId { get; set; }

    public int ChatConversationId { get; set; }

    public string UserQuestion { get; set; } = null!;

    public string AiAnswer { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public ChatConversation ChatConversation { get; set; } = null!;

    // Citation tách riêng để UI hiển thị nguồn mà không trộn vào nội dung trả lời.
    public ICollection<ChatCitation> ChatCitations { get; set; } = new List<ChatCitation>();
}
