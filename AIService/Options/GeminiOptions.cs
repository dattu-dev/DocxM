namespace AIService.Options;

public sealed class GeminiOptions
{
    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public string ChatModel { get; set; } = "gemini-2.5-flash";

    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    public int MaxOutputTokens { get; set; } = 512;

    public double Temperature { get; set; } = 0.1;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
