
namespace Elovo.Application.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResultDto> VerifyTwoFactorAsync(Guid userId, string code, string? clientIp, CancellationToken cancellationToken = default);
}
