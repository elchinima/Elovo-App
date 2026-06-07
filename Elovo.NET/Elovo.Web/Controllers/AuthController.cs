namespace Elovo.Web.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private const string AuthCookieName = "ElovoAuthToken";
    private const string TwoFactorCookieName = "ElovoTwoFactorUser";
    private const string GoogleStateCookieName = "ElovoGoogleOAuthState";
    private const string GoogleAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IValidator<LoginDto> loginValidator,
        IValidator<RegisterDto> registerValidator,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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

    [HttpGet("callback")]
    [AllowAnonymous]
    public IActionResult Callback()
    {
        return RedirectToAction("Index", "Chat");
    }

    [HttpGet("~/google-login")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Chat");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            TempData["AuthError"] = "Google sign-in was cancelled or denied.";
            return RedirectToAction(nameof(Login));
        }

        var clientId = GetGoogleClientId();
        var clientSecret = GetGoogleClientSecret();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            TempData["AuthError"] = "Google sign-in is not configured.";
            return RedirectToAction(nameof(Login));
        }

        var redirectUri = GetGoogleRedirectUri();
        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToGoogle(clientId, redirectUri);
        }

        if (!ValidateGoogleState(state))
        {
            TempData["AuthError"] = "Google sign-in could not be verified. Try again.";
            return RedirectToAction(nameof(Login));
        }

        Response.Cookies.Delete(GoogleStateCookieName);

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var idToken = await ExchangeGoogleCodeForIdTokenAsync(code, clientId, clientSecret, redirectUri, cancellationToken);
            if (string.IsNullOrWhiteSpace(idToken))
            {
                TempData["AuthError"] = "Google sign-in response is missing.";
                return RedirectToAction(nameof(Login));
            }

            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
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
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Response.Cookies.Append(AuthCookieName, "", cookieOptions);
        Response.Cookies.Append(TwoFactorCookieName, "", cookieOptions);

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

    private string? GetGoogleClientSecret()
    {
        return _configuration["GoogleAuth:ClientSecret"];
    }

    private IActionResult RedirectToGoogle(string clientId, string redirectUri)
    {
        var state = CreateGoogleState();
        Response.Cookies.Append(GoogleStateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["prompt"] = "select_account"
        };

        var authorizationUrl = GoogleAuthorizationEndpoint + "?" + string.Join("&", query.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));

        return Redirect(authorizationUrl);
    }

    private async Task<string?> ExchangeGoogleCodeForIdTokenAsync(string code, string clientId, string clientSecret, string redirectUri, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _httpClientFactory
            .CreateClient()
            .PostAsync(GoogleTokenEndpoint, content, cancellationToken);

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google token exchange failed with status {StatusCode}: {Response}", response.StatusCode, responseJson);
            return null;
        }

        using var document = JsonDocument.Parse(responseJson);
        return document.RootElement.TryGetProperty("id_token", out var idTokenElement)
            ? idTokenElement.GetString()
            : null;
    }

    private string GetGoogleRedirectUri()
    {
        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
        return $"{Request.Scheme}://{Request.Host}{pathBase}/google-login";
    }

    private bool ValidateGoogleState(string? state)
    {
        return !string.IsNullOrWhiteSpace(state) &&
            Request.Cookies.TryGetValue(GoogleStateCookieName, out var stateCookie) &&
            stateCookie == state;
    }

    private static string CreateGoogleState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes);
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
