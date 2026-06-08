
namespace Elovo.Application.Services;

public class AuthService : IAuthService
{
    private static readonly TimeSpan TwoFactorCodeLifetime = TimeSpan.FromMinutes(15);

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
            TwoFactor = new UserTwoFactor()
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
        if (twoFactor.IsTwoFactorEnabled && !string.IsNullOrWhiteSpace(user.Email))
        {
            var code = GenerateTwoFactorCode();
            twoFactor.TwoFactorCodeHash = BCrypt.Net.BCrypt.HashPassword(code);
            twoFactor.TwoFactorCodeExpiredAt = DateTime.UtcNow.Add(TwoFactorCodeLifetime);
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _emailSender.SendTwoFactorCodeAsync(user.Email, user.Username, code, cancellationToken);
            return AuthResultDto.TwoFactorRequired(user);
        }
        var session = EnsureSession(user);
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

    private static UserSession EnsureSession(User user)
    {
        return user.Session ??= new UserSession { UserId = user.Id };
    }

    private static UserTwoFactor EnsureTwoFactor(User user)
    {
        return user.TwoFactor ??= new UserTwoFactor { UserId = user.Id };
    }
}
