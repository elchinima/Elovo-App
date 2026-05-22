using Elovo.Application.DTOs;
using Elovo.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elovo.Web.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private const string AuthCookieName = "ElovoAuthToken";
    private readonly IAuthService _authService;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<RegisterDto> _registerValidator;

    public AuthController(
        IAuthService authService,
        IValidator<LoginDto> loginValidator,
        IValidator<RegisterDto> registerValidator)
    {
        _authService = authService;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        return User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Chat") : View();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            ViewBag.Error = "Invalid username or password.";
            return View(dto);
        }

        var result = await _authService.LoginAsync(dto, cancellationToken);
        if (!result.Succeeded || result.Token is null)
        {
            ViewBag.Error = result.Error ?? "Invalid username or password.";
            return View(dto);
        }

        SetAuthCookie(result.Token);
        return RedirectToAction("Index", "Chat");
    }

    [HttpGet("register")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Chat") : View();
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken cancellationToken)
    {
        var validation = await _registerValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            ViewBag.Error = validation.Errors.First().ErrorMessage;
            return View(dto);
        }

        var result = await _authService.RegisterAsync(dto, cancellationToken);
        if (!result.Succeeded || result.Token is null)
        {
            ViewBag.Error = result.Error ?? "Registration failed.";
            return View(dto);
        }

        SetAuthCookie(result.Token);
        return RedirectToAction("Index", "Chat");
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookieName);
        return RedirectToAction(nameof(Login));
    }

    private void SetAuthCookie(string token)
    {
        Response.Cookies.Append(AuthCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
