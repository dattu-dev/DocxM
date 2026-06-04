namespace AIService.Services;

public interface IAnswerGenerationService
{
    // Nhận prompt RAG đã build và trả về câu trả lời cuối cùng cho người dùng.
    Task<string> GenerateAnswerAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
