using AIService.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AIService;

public static class DependencyInjection
{
    public static IServiceCollection AddAiService(this IServiceCollection services)
    {
        services.AddScoped<IFileSignatureValidator, FileSignatureValidator>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddScoped<IEmbeddingService, DeterministicEmbeddingService>();
        services.AddScoped<IVectorStore, LocalJsonVectorStore>();

        return services;
    }
}
