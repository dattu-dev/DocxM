namespace AIService.Services;

public interface IAnswerGenerationService
{
    Task<string> GenerateAnswerAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
