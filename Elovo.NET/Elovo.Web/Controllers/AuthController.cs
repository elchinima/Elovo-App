namespace Elovo.Web.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private const string AuthCookieName = "ElovoAuthToken";
    private const string TwoFactorCookieName = "ElovoTwoFactorUser";
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IConfiguration configuration,
        IValidator<LoginDto> loginValidator,
        IValidator<RegisterDto> registerValidator,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _configuration = configuration;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _logger = logger;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (TempData.TryGetValue("AuthError", out var authError))
        {
            ViewBag.Error = authError;
        }

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

        AuthResultDto result;
        try
        {
            result = await _authService.LoginAsync(dto, ClientIpAddressResolver.Resolve(HttpContext), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            ViewBag.Error = "Could not send the verification code. Try again later.";
            return View(dto);
        }

        if (result.RequiresTwoFactor && result.TwoFactorUserId is not null)
        {
            SetTwoFactorCookie(result.TwoFactorUserId.Value);
            TempData["TwoFactorEmail"] = MaskEmail(result.TwoFactorEmail);
            return RedirectToAction(nameof(TwoFactor));
        }

        if (!result.Succeeded || result.Token is null)
        {
            ViewBag.Error = result.Error ?? "Invalid username or password.";
            return View(dto);
        }

        SetAuthCookie(result.Token);
        return RedirectToAction("Index", "Chat");
    }

    [HttpGet("~/google-login")]
    [AllowAnonymous]
    public IActionResult GoogleLogin()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Chat");
        }

        ViewBag.GoogleClientId = GetGoogleClientId();
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
        ViewBag.GoogleLoginUri = $"{Request.Scheme}://{Request.Host}{pathBase}/google-login";
        return View();
    }

    [HttpPost("~/google-login")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GoogleLoginCallback([FromForm] string? credential, [FromForm(Name = "g_csrf_token")] string? csrfToken, CancellationToken cancellationToken)
    {
        if (!ValidateGoogleCsrfToken(csrfToken))
        {
            TempData["AuthError"] = "Google sign-in could not be verified. Try again.";
            return RedirectToAction(nameof(Login));
        }

        var clientId = GetGoogleClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            TempData["AuthError"] = "Google sign-in is not configured.";
            return RedirectToAction(nameof(Login));
        }

        if (string.IsNullOrWhiteSpace(credential))
        {
            TempData["AuthError"] = "Google sign-in response is missing.";
            return RedirectToAction(nameof(Login));
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(credential, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google sign-in token validation failed");
            TempData["AuthError"] = "Google sign-in could not be verified. Try again.";
            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Google sign-in validation error");
            TempData["AuthError"] = "Google sign-in is unavailable. Try again later.";
            return RedirectToAction(nameof(Login));
        }

        if (payload.EmailVerified != true || string.IsNullOrWhiteSpace(payload.Email))
        {
            TempData["AuthError"] = "Google account email is not verified.";
            return RedirectToAction(nameof(Login));
        }

        var result = await _authService.LoginWithGoogleAsync(payload.Email, ClientIpAddressResolver.Resolve(HttpContext), cancellationToken);
        if (!result.Succeeded || result.Token is null)
        {
            TempData["AuthError"] = result.Error ?? "No user account exists with this Google email.";
            return RedirectToAction(nameof(Login));
        }

        SetAuthCookie(result.Token);
        return RedirectToAction("Index", "Chat");
    }

    [HttpGet("two-factor")]
    [AllowAnonymous]
    public IActionResult TwoFactor()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Chat");
        }

        if (!TryGetTwoFactorUserId(out _))
        {
            return RedirectToAction(nameof(Login));
        }

        ViewBag.Email = TempData.Peek("TwoFactorEmail");
        return View(new VerifyTwoFactorDto());
    }

    [HttpPost("two-factor")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactor(VerifyTwoFactorDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetTwoFactorUserId(out var userId))
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _authService.VerifyTwoFactorAsync(userId, dto.Code, ClientIpAddressResolver.Resolve(HttpContext), cancellationToken);
        if (!result.Succeeded || result.Token is null)
        {
            ViewBag.Error = result.Error ?? "Verification failed.";
            ViewBag.Email = TempData.Peek("TwoFactorEmail");
            return View(dto);
        }

        Response.Cookies.Delete(TwoFactorCookieName);
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

        var result = await _authService.RegisterAsync(dto, ClientIpAddressResolver.Resolve(HttpContext), cancellationToken);
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
        Response.Cookies.Delete(TwoFactorCookieName);
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

    private void SetTwoFactorCookie(Guid userId)
    {
        Response.Cookies.Append(TwoFactorCookieName, userId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(15)
        });
    }

    private bool TryGetTwoFactorUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return Request.Cookies.TryGetValue(TwoFactorCookieName, out var value) &&
            Guid.TryParse(value, out userId);
    }

    private string? GetGoogleClientId()
    {
        return _configuration["GoogleAuth:ClientId"];
    }

    private bool ValidateGoogleCsrfToken(string? csrfToken)
    {
        return !string.IsNullOrWhiteSpace(csrfToken) &&
            Request.Cookies.TryGetValue("g_csrf_token", out var csrfCookie) &&
            csrfCookie == csrfToken;
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return string.Empty;
        }

        var parts = email.Split('@', 2);
        var name = parts[0];
        if (string.IsNullOrEmpty(name))
        {
            return $"***@{parts[1]}";
        }

        var visible = name.Length <= 2 ? name[..1] : name[..2];
        return $"{visible}***@{parts[1]}";
    }
}
