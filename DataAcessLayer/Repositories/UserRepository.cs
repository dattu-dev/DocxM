using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AppUser?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return BuildUserQuery()
            .FirstOrDefaultAsync(user => user.UserId == userId, cancellationToken);
    }

    public Task<AppUser?> GetByUserNameOrEmailAsync(
        string userNameOrEmail,
        CancellationToken cancellationToken = default)
    {
        // Đăng nhập dùng giá trị normalized để username/email không phụ thuộc chữ hoa thường.
        string normalizedValue = Normalize(userNameOrEmail);

        return BuildUserQuery()
            .FirstOrDefaultAsync(
                user => user.NormalizedUserName == normalizedValue ||
                        user.NormalizedEmail == normalizedValue,
                cancellationToken);
    }

    public Task<AppUser?> GetByNormalizedUserNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken = default)
    {
        return BuildUserQuery()
            .FirstOrDefaultAsync(
                user => user.NormalizedUserName == normalizedUserName,
                cancellationToken);
    }

    public Task<AppUser?> GetByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return BuildUserQuery()
            .FirstOrDefaultAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken);
    }

    public async Task AddUserAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await _context.AppUsers.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AppUser> BuildUserQuery()
    {
        return _context.AppUsers;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
