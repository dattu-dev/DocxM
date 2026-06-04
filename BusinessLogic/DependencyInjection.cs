using BusinessLogic.Options;
using BusinessLogic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(
        this IServiceCollection services,
        RagDebugOptions? ragDebugOptions = null)
    {
        // Đăng ký service theo scoped để mỗi request dùng cùng vòng đời với DbContext.
        services.AddSingleton(ragDebugOptions ?? new RagDebugOptions());
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
