using BusinessObjects.Entities;

namespace DataAcessLayer.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task AddUserAsync(AppUser user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
