namespace AIService.Models;

public sealed record VectorRecord(
    string VectorId,
    float[] EmbeddingVector,
    int DocumentId,
    long ChunkId);
