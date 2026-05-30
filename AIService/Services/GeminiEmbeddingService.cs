using AIService.Models;
using AIService.Options;

namespace AIService.Services;

public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private readonly GeminiApiClient _client;
    private readonly GeminiOptions _options;
    private readonly DeterministicEmbeddingService _fallback = new();

    public GeminiEmbeddingService(GeminiApiClient client, GeminiOptions options)
    {
        _client = client;
        _options = options;
    }

    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return GenerateEmbeddingAsync(text, EmbeddingTaskType.RetrievalDocument, cancellationToken);
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingTaskType taskType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string model = NormalizeModelName(_options.EmbeddingModel);
            string endpoint = $"{model}:embedContent";
            var request = new
            {
                content = new
                {
                    parts = new[]
                    {
                        new { text = text ?? string.Empty }
                    }
                },
                taskType = MapTaskType(taskType)
            };

            using var response = await _client.PostAsync(endpoint, request, cancellationToken);

            if (!response.RootElement.TryGetProperty("embedding", out var embedding) ||
                !embedding.TryGetProperty("values", out var values) ||
                values.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                throw new InvalidOperationException("Gemini embedding response is missing embedding.values.");
            }

            return values
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"[Gemini] Embedding failed, falling back to local embedding. {exception.Message}");
            System.Diagnostics.Debug.WriteLine(exception.ToString());

            return await _fallback.GenerateEmbeddingAsync(text, taskType, cancellationToken);
        }
    }

    private static string MapTaskType(EmbeddingTaskType taskType)
    {
        return taskType == EmbeddingTaskType.RetrievalQuery
            ? "RETRIEVAL_QUERY"
            : "RETRIEVAL_DOCUMENT";
    }

    private static string NormalizeModelName(string model)
    {
        string normalized = string.IsNullOrWhiteSpace(model)
            ? "gemini-embedding-001"
            : model.Trim();

        return normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"models/{normalized}";
    }
}
