
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
            RegistrationIp = clientIp,
            LastLoginIp = clientIp
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

        if (user.IsTwoFactorEnabled && !string.IsNullOrWhiteSpace(user.Email))
        {
            var code = GenerateTwoFactorCode();
            user.TwoFactorCodeHash = BCrypt.Net.BCrypt.HashPassword(code);
            user.TwoFactorCodeExpiresAt = DateTime.UtcNow.Add(TwoFactorCodeLifetime);
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _emailSender.SendTwoFactorCodeAsync(user.Email, user.Username, code, cancellationToken);
            return AuthResultDto.TwoFactorRequired(user);
        }

        user.TwoFactorCodeHash = null;
        user.TwoFactorCodeExpiresAt = null;
        user.IsOnline = true;
        user.LastSeenAt = null;
        ApplyLoginIp(user, clientIp);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Success(_mapper.Map<UserDto>(user), CreateToken(user));
    }

    public async Task<AuthResultDto> VerifyTwoFactorAsync(Guid userId, string code, string? clientIp, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.TwoFactorCodeHash) || user.TwoFactorCodeExpiresAt is null)
        {
            return AuthResultDto.Failure("Verification code is invalid.");
        }

        if (user.TwoFactorCodeExpiresAt <= DateTime.UtcNow)
        {
            ClearTwoFactorCode(user);
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthResultDto.Failure("Verification code expired.");
        }

        var normalizedCode = NormalizeTwoFactorCode(code);
        if (normalizedCode.Length != 7 || !BCrypt.Net.BCrypt.Verify(normalizedCode, user.TwoFactorCodeHash))
        {
            return AuthResultDto.Failure("Verification code is invalid.");
        }

        ClearTwoFactorCode(user);
        user.IsOnline = true;
        user.LastSeenAt = null;
        ApplyLoginIp(user, clientIp);
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

    private static void ClearTwoFactorCode(User user)
    {
        user.TwoFactorCodeHash = null;
        user.TwoFactorCodeExpiresAt = null;
    }

    private static void ApplyLoginIp(User user, string? clientIp)
    {
        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            user.LastLoginIp = clientIp;
        }

        if (string.IsNullOrWhiteSpace(user.RegistrationIp))
        {
            user.RegistrationIp = user.LastLoginIp;
        }
    }
}
