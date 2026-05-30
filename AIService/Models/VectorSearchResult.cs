namespace AIService.Models;

public sealed record VectorSearchResult(
    VectorRecord Vector,
    double Score);
