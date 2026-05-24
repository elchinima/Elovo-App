
namespace Elovo.Application.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    Task<AuthResultDto> VerifyTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken = default);
}
