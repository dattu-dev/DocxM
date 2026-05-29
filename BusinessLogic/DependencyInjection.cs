using BusinessLogic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IDocumentService, DocumentService>();

        return services;
    }
}
