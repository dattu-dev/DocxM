namespace AIService.Services;

using AIService.Models;

public interface IEmbeddingService
{
    // Embedding biến document hoặc câu hỏi thành vector để tìm chunk liên quan.
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingTaskType taskType,
        CancellationToken cancellationToken = default);
}
