using System.Text;
using AIService.Options;

namespace AIService.Services;

public sealed class GeminiAnswerGenerationService : IAnswerGenerationService
{
    private const string FallbackAnswer = "Tài liệu chưa có thông tin phù hợp.";

    private readonly GeminiApiClient _client;
    private readonly GeminiOptions _options;
    private readonly PromptAnswerGenerationService _fallback = new();

    public GeminiAnswerGenerationService(GeminiApiClient client, GeminiOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<string> GenerateAnswerAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        string model = NormalizeModelName(_options.ChatModel);
        string endpoint = $"{model}:generateContent";
        var request = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = _options.Temperature,
                maxOutputTokens = _options.MaxOutputTokens
            }
        };

        try
        {
            // Gọi Gemini API thật để sinh câu trả lời từ prompt RAG.
            using var response = await _client.PostAsync(endpoint, request, cancellationToken);
            string answer = ExtractAnswer(response.RootElement);

            return string.IsNullOrWhiteSpace(answer)
                ? FallbackAnswer
                : answer.Trim();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Fallback local giúp hệ thống vẫn trả lời được khi Gemini không khả dụng.
            Console.WriteLine($"[Gemini] Answer generation failed, falling back to local answer generator. {exception.Message}");
            System.Diagnostics.Debug.WriteLine(exception.ToString());

            return await _fallback.GenerateAnswerAsync(prompt, cancellationToken);
        }
    }

    private static string ExtractAnswer(System.Text.Json.JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement))
                {
                    builder.AppendLine(textElement.GetString());
                }
            }

            if (builder.Length > 0)
            {
                break;
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeModelName(string model)
    {
        string normalized = string.IsNullOrWhiteSpace(model)
            ? "gemini-2.5-flash"
            : model.Trim();

        return normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"models/{normalized}";
    }
}
