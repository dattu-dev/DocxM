namespace BusinessLogic.DTOs;

public sealed record LoginDto(
    string UserNameOrEmail,
    string Password);

public sealed record RegisterDto(
    string UserName,
    string Email,
    string FullName,
    string Password,
    string ConfirmPassword);

public sealed record AuthenticatedUserDto(
    int UserId,
    string UserName,
    string Email,
    string FullName,
    string Role);
