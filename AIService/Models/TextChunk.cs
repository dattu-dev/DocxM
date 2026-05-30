namespace AIService.Models;

public sealed record TextChunk(
    int Index,
    string Content,
    int EstimatedTokenCount,
    string? SectionTitle);
