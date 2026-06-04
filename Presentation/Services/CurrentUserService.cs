using System.Security.Claims;
using BusinessObjects;

namespace Presentation.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int UserId
    {
        get
        {
            // Mọi kiểm quyền phía Presentation dựa trên claim user id từ cookie đăng nhập.
            string? userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out int parsedUserId))
            {
                throw new InvalidOperationException("Không xác định được người dùng hiện tại.");
            }

            return parsedUserId;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public bool IsInstructor => User?.IsInRole(UserRoles.Instructor) == true;

    public bool IsStudent => User?.IsInRole(UserRoles.Student) == true;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
}
