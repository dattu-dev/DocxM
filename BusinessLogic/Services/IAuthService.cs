using BusinessLogic.DTOs;

namespace BusinessLogic.Services;

public interface IAuthService
{
    Task<AuthenticatedUserDto> RegisterAsync(RegisterDto register, CancellationToken cancellationToken = default);

    Task<AuthenticatedUserDto> LoginAsync(LoginDto login, CancellationToken cancellationToken = default);
}
