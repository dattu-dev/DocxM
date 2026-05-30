namespace BusinessObjects.Entities;

public sealed class ChatMessage
{
    public long ChatMessageId { get; set; }

    public int ChatConversationId { get; set; }

    public string UserQuestion { get; set; } = null!;

    public string AiAnswer { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public ChatConversation ChatConversation { get; set; } = null!;

    public ICollection<ChatCitation> ChatCitations { get; set; } = new List<ChatCitation>();
}
