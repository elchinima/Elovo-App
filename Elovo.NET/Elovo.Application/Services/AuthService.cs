
namespace Elovo.Application.Services;

public class AuthService : IAuthService
{
    private static readonly TimeSpan TwoFactorCodeLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan EmailSendCooldown = TimeSpan.FromHours(1);

    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(IUnitOfWork unitOfWork, IMapper mapper, IConfiguration configuration, IEmailSender emailSender)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _configuration = configuration;
        _emailSender = emailSender;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterDto dto, string? clientIp, CancellationToken cancellationToken = default)
    {
        var username = dto.Username.Trim();
        var existingUser = await _unitOfWork.Users.GetByUsernameAsync(username, cancellationToken);
        if (existingUser is not null)
        {
            return AuthResultDto.Failure("Username already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow,
            Session = new UserSession
            {
                RegistrationIp = clientIp,
                LastLoginIp = clientIp
            },
            TwoFactor = new UserTwoFactor(),
            EmailSettings = new UserEmail()
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Success(_mapper.Map<UserDto>(user), CreateToken(user));
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto, string? clientIp, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByUsernameAsync(dto.Username.Trim(), cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return AuthResultDto.Failure("Invalid username or password.");
        }

        var twoFactor = EnsureTwoFactor(user);
        var session = EnsureSession(user);
        var emailSettings = user.EmailSettings;
        ApplyPreferredLanguage(session, dto.PreferredLanguage);
        if (twoFactor.IsTwoFactorEnabled && !string.IsNullOrWhiteSpace(emailSettings?.Email))
        {
            var cooldownEndsAt = GetEmailCooldownEndsAt(emailSettings);
            var hasValidCode = !string.IsNullOrWhiteSpace(twoFactor.TwoFactorCodeHash) &&
                twoFactor.TwoFactorCodeExpiredAt > DateTime.UtcNow;

            if (cooldownEndsAt is not null && !hasValidCode)
            {
                return AuthResultDto.Failure(
                    BuildEmailCooldownMessage(cooldownEndsAt.Value, dto.PreferredLanguage),
                    cooldownEndsAt.Value);
            }

            if (cooldownEndsAt is null)
            {
                var code = GenerateTwoFactorCode();
                await _emailSender.SendTwoFactorCodeAsync(emailSettings.Email, user.Username, code, dto.PreferredLanguage, cancellationToken);
                twoFactor.TwoFactorCodeHash = BCrypt.Net.BCrypt.HashPassword(code);
                twoFactor.TwoFactorCodeExpiredAt = DateTime.UtcNow.Add(TwoFactorCodeLifetime);
                emailSettings.LastEmailSentAt = DateTime.UtcNow;
            }

            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthResultDto.TwoFactorRequired(user, GetEmailCooldownEndsAt(emailSettings));
        }
        twoFactor.TwoFactorCodeHash = null;
        twoFactor.TwoFactorCodeExpiredAt = null;
        ApplyLoginIp(session, clientIp);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Success(_mapper.Map<UserDto>(user), CreateToken(user));
    }

    public async Task<AuthResultDto> LoginWithGoogleAsync(string email, string? clientIp, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return AuthResultDto.Failure("Google account email is missing.");
        }

        var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return AuthResultDto.Failure("No user account exists with this Google email.");
        }

        var twoFactor = EnsureTwoFactor(user);
        var session = EnsureSession(user);
        var emailSettings = EnsureEmailSettings(user);
        emailSettings.IsEmailConfirmed = true;
        ClearEmailConfirmationCode(emailSettings);
        twoFactor.TwoFactorCodeHash = null;
        twoFactor.TwoFactorCodeExpiredAt = null;
        ApplyLoginIp(session, clientIp);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Success(_mapper.Map<UserDto>(user), CreateToken(user));
    }

    public async Task<AuthResultDto> VerifyTwoFactorAsync(Guid userId, string code, string? clientIp, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        var twoFactor = user?.TwoFactor;
        if (user is null || twoFactor is null || string.IsNullOrWhiteSpace(twoFactor.TwoFactorCodeHash) || twoFactor.TwoFactorCodeExpiredAt is null)
        {
            return AuthResultDto.Failure("Verification code is invalid.");
        }

        if (twoFactor.TwoFactorCodeExpiredAt <= DateTime.UtcNow)
        {
            ClearTwoFactorCode(twoFactor);
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthResultDto.Failure("Verification code expired.");
        }

        var normalizedCode = NormalizeTwoFactorCode(code);
        if (normalizedCode.Length != 7 || !BCrypt.Net.BCrypt.Verify(normalizedCode, twoFactor.TwoFactorCodeHash))
        {
            return AuthResultDto.Failure("Verification code is invalid.");
        }

        var session = EnsureSession(user);
        if (user.EmailSettings is not null)
        {
            user.EmailSettings.IsEmailConfirmed = true;
            ClearEmailConfirmationCode(user.EmailSettings);
        }
        ClearTwoFactorCode(twoFactor);
        session.IsOnline = true;
        session.LastSeenAt = null;
        ApplyLoginIp(session, clientIp);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Success(_mapper.Map<UserDto>(user), CreateToken(user));
    }

    private string CreateToken(User user)
    {
        var secret = GetRequiredConfigurationValue("Jwt:Secret");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryDays = int.TryParse(_configuration["Jwt:ExpiryDays"], out var days) ? days : 7;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GetRequiredConfigurationValue(string key)
    {
        var value = _configuration[key];
        return string.IsNullOrWhiteSpace(value) || value.StartsWith("Set via ", StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidOperationException($"{key} is not configured.")
            : value;
    }

    private static string GenerateTwoFactorCode()
    {
        return System.Security.Cryptography.RandomNumberGenerator
            .GetInt32(1_000_000, 10_000_000)
            .ToString();
    }

    private static string NormalizeTwoFactorCode(string code)
    {
        return new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static string NormalizeEmail(string email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static void ClearTwoFactorCode(UserTwoFactor twoFactor)
    {
        twoFactor.TwoFactorCodeHash = null;
        twoFactor.TwoFactorCodeExpiredAt = null;
    }

    private static void ClearEmailConfirmationCode(UserEmail emailSettings)
    {
        emailSettings.EmailConfirmationCodeHash = null;
        emailSettings.EmailConfirmationCodeExpiredAt = null;
    }

    private static DateTime? GetEmailCooldownEndsAt(UserEmail emailSettings)
    {
        if (emailSettings.LastEmailSentAt is null)
        {
            return null;
        }

        var endsAt = emailSettings.LastEmailSentAt.Value.Add(EmailSendCooldown);
        return endsAt > DateTime.UtcNow ? endsAt : null;
    }

    private static string BuildEmailCooldownMessage(DateTime cooldownEndsAt, string? language)
    {
        var remaining = cooldownEndsAt - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        var time = $"{minutes:D2}:{seconds:D2}";

        return NormalizeLanguage(language) switch
        {
            "ru" => $"\u0421\u043b\u0435\u0434\u0443\u044e\u0449\u0435\u0435 \u043f\u0438\u0441\u044c\u043c\u043e \u043c\u043e\u0436\u043d\u043e \u0437\u0430\u043f\u0440\u043e\u0441\u0438\u0442\u044c \u0447\u0435\u0440\u0435\u0437 {time}.",
            "az" => $"N\u00f6vb\u0259ti e-po\u00e7tu {time} sonra ist\u0259y\u0259 bil\u0259rsiniz.",
            _ => $"You can request another email in {time}."
        };
    }

    private static void ApplyLoginIp(UserSession session, string? clientIp)
    {
        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            session.LastLoginIp = clientIp;
        }

        if (string.IsNullOrWhiteSpace(session.RegistrationIp))
        {
            session.RegistrationIp = session.LastLoginIp;
        }
    }

    private static void ApplyPreferredLanguage(UserSession session, string? language)
    {
        var normalized = NormalizeLanguage(language);
        if (normalized is "en" or "ru" or "az")
        {
            session.PreferredLanguage = normalized;
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        return (language ?? string.Empty).Trim().ToLowerInvariant().Split('-')[0];
    }

    private static UserSession EnsureSession(User user)
    {
        return user.Session ??= new UserSession { UserId = user.Id };
    }

    private static UserTwoFactor EnsureTwoFactor(User user)
    {
        return user.TwoFactor ??= new UserTwoFactor { UserId = user.Id };
    }

    private static UserEmail EnsureEmailSettings(User user)
    {
        return user.EmailSettings ??= new UserEmail { UserId = user.Id };
    }
}
