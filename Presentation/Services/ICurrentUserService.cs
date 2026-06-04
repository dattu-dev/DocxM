namespace Presentation.Services;

public interface ICurrentUserService
{
    int UserId { get; }

    string? Email { get; }

    string? Role { get; }

    bool IsAuthenticated { get; }

    bool IsInstructor { get; }

    bool IsStudent { get; }
}
