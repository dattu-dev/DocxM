using System.Text.RegularExpressions;
using BusinessLogic.DTOs;
using BusinessLogic.Validation;
using BusinessObjects;
using BusinessObjects.Entities;
using DataAcessLayer.Repositories;

namespace BusinessLogic.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthenticatedUserDto> RegisterAsync(
        RegisterDto register,
        CancellationToken cancellationToken = default)
    {
        await ValidateRegisterAsync(register, cancellationToken);

        var user = new AppUser
        {
            UserName = register.UserName.Trim(),
            NormalizedUserName = Normalize(register.UserName),
            Email = register.Email.Trim(),
            NormalizedEmail = Normalize(register.Email),
            FullName = register.FullName.Trim(),
            // User mới luôn là Student để tránh tự nâng quyền Instructor từ form đăng ký.
            Role = UserRoles.Student,
            PasswordHash = _passwordHasher.HashPassword(register.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddUserAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapAuthenticatedUser(user);
    }

    public async Task<AuthenticatedUserDto> LoginAsync(
        LoginDto login,
        CancellationToken cancellationToken = default)
    {
        ValidateLogin(login);

        AppUser? user = await _userRepository.GetByUserNameOrEmailAsync(
            login.UserNameOrEmail,
            cancellationToken);

        if (user is null || !user.IsActive || !_passwordHasher.VerifyPassword(login.Password, user.PasswordHash))
        {
            throw new BusinessValidationException(
            [
                new ValidationError(string.Empty, "Tên đăng nhập/email hoặc mật khẩu không đúng.")
            ]);
        }

        return MapAuthenticatedUser(user);
    }

    private async Task ValidateRegisterAsync(
        RegisterDto register,
        CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();

        ValidateText(errors, nameof(register.UserName), register.UserName, "Tên đăng nhập", 3, 100);
        ValidateText(errors, nameof(register.FullName), register.FullName, "Họ tên", 2, 150);
        ValidateText(errors, nameof(register.Email), register.Email, "Email", 5, 256);
        ValidateText(errors, nameof(register.Password), register.Password, "Mật khẩu", 6, 100);

        if (!string.IsNullOrWhiteSpace(register.UserName) &&
            !Regex.IsMatch(register.UserName.Trim(), "^[a-zA-Z0-9_.-]+$"))
        {
            errors.Add(new ValidationError(nameof(register.UserName), "Tên đăng nhập chỉ được chứa chữ, số, dấu gạch dưới, dấu chấm hoặc dấu gạch ngang."));
        }

        if (!string.IsNullOrWhiteSpace(register.Email) &&
            !Regex.IsMatch(register.Email.Trim(), "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"))
        {
            errors.Add(new ValidationError(nameof(register.Email), "Email không hợp lệ."));
        }

        if (!string.Equals(register.Password, register.ConfirmPassword, StringComparison.Ordinal))
        {
            errors.Add(new ValidationError(nameof(register.ConfirmPassword), "Xác nhận mật khẩu không khớp."));
        }

        if (!string.IsNullOrWhiteSpace(register.UserName) &&
            await _userRepository.GetByNormalizedUserNameAsync(Normalize(register.UserName), cancellationToken) is not null)
        {
            errors.Add(new ValidationError(nameof(register.UserName), "Tên đăng nhập đã tồn tại."));
        }

        if (!string.IsNullOrWhiteSpace(register.Email) &&
            await _userRepository.GetByNormalizedEmailAsync(Normalize(register.Email), cancellationToken) is not null)
        {
            errors.Add(new ValidationError(nameof(register.Email), "Email đã tồn tại."));
        }

        if (errors.Count > 0)
        {
            throw new BusinessValidationException(errors);
        }
    }

    private static void ValidateLogin(LoginDto login)
    {
        var errors = new List<ValidationError>();

        ValidateText(errors, nameof(login.UserNameOrEmail), login.UserNameOrEmail, "Tên đăng nhập hoặc email", 3, 256);
        ValidateText(errors, nameof(login.Password), login.Password, "Mật khẩu", 1, 100);

        if (errors.Count > 0)
        {
            throw new BusinessValidationException(errors);
        }
    }

    private static void ValidateText(
        ICollection<ValidationError> errors,
        string fieldName,
        string? value,
        string label,
        int minLength,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(fieldName, $"{label} là bắt buộc."));
            return;
        }

        string trimmedValue = value.Trim();

        if (trimmedValue.Length < minLength || trimmedValue.Length > maxLength)
        {
            errors.Add(new ValidationError(fieldName, $"{label} phải từ {minLength} đến {maxLength} ký tự."));
        }
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return UserRoles.Student;
        }

        string trimmedRole = role.Trim();

        if (string.Equals(trimmedRole, UserRoles.Instructor, StringComparison.OrdinalIgnoreCase))
        {
            return UserRoles.Instructor;
        }

        return string.Equals(trimmedRole, UserRoles.Student, StringComparison.OrdinalIgnoreCase)
            ? UserRoles.Student
            : trimmedRole;
    }

    private static AuthenticatedUserDto MapAuthenticatedUser(AppUser user)
    {
        return new AuthenticatedUserDto(
            user.UserId,
            user.UserName,
            user.Email,
            user.FullName,
            NormalizeRole(user.Role));
    }
}
