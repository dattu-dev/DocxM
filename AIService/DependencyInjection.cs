using AIService.Services;
using AIService.Options;
using Microsoft.Extensions.DependencyInjection;

namespace AIService;

public static class DependencyInjection
{
    public static IServiceCollection AddAiService(
        this IServiceCollection services,
        GeminiOptions? geminiOptions = null)
    {
        geminiOptions ??= new GeminiOptions();

        services.AddScoped<IFileSignatureValidator, FileSignatureValidator>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddScoped<IVectorStore, LocalJsonVectorStore>();
        services.AddSingleton(geminiOptions);

        if (geminiOptions.IsConfigured)
        {
            // Khi có Gemini config hợp lệ, dùng Gemini cho embedding và sinh câu trả lời.
            services.AddSingleton<GeminiApiClient>();
            services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
            services.AddScoped<IAnswerGenerationService, GeminiAnswerGenerationService>();
        }
        else
        {
            // Fallback local giúp môi trường dev/demo chạy được mà không cần API key.
            services.AddScoped<IEmbeddingService, DeterministicEmbeddingService>();
            services.AddScoped<IAnswerGenerationService, PromptAnswerGenerationService>();
        }

        return services;
    }
}
