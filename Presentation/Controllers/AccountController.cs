using System.Security.Claims;
using BusinessLogic.DTOs;
using BusinessLogic.Services;
using BusinessLogic.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;

namespace Presentation.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectAfterLogin(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        try
        {
            AuthenticatedUserDto user = await _authService.LoginAsync(
                new LoginDto(viewModel.UserNameOrEmail, viewModel.Password),
                cancellationToken);

            await SignInUserAsync(user);

            return RedirectAfterLogin(viewModel.ReturnUrl);
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);
            return View(viewModel);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        var viewModel = new RegisterViewModel();

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        try
        {
            AuthenticatedUserDto user = await _authService.RegisterAsync(
                new RegisterDto(
                    viewModel.UserName,
                    viewModel.Email,
                    viewModel.FullName,
                    viewModel.Password,
                    viewModel.ConfirmPassword),
                cancellationToken);

            await SignInUserAsync(user);

            return RedirectAfterLogin(null);
        }
        catch (BusinessValidationException ex)
        {
            AddValidationErrors(ex);

            return View(viewModel);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInUserAsync(AuthenticatedUserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new("FullName", user.FullName)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });
    }

    private IActionResult RedirectAfterLogin(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Subject");
    }

    private void AddValidationErrors(BusinessValidationException exception)
    {
        foreach (ValidationError error in exception.Errors)
        {
            ModelState.AddModelError(error.FieldName, error.ErrorMessage);
        }
    }
}
