using System.ComponentModel.DataAnnotations;
namespace Presentation.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email.")]
    [Display(Name = "Tên đăng nhập hoặc email")]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 100 ký tự.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 150 ký tự.")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;

}
