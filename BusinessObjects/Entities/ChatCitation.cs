namespace BusinessObjects.Entities;

public sealed class ChatCitation
{
    public long ChatCitationId { get; set; }

    public long ChatMessageId { get; set; }

    public int DocumentId { get; set; }

    public long DocumentChunkId { get; set; }

    public string DocumentName { get; set; } = null!;

    public int? PageNumber { get; set; }

    public string Snippet { get; set; } = null!;

    public double SimilarityScore { get; set; }

    public ChatMessage ChatMessage { get; set; } = null!;
}
