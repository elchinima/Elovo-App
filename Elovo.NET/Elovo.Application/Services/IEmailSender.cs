namespace Elovo.Application.Services;

public interface IEmailSender
{
    Task SendTwoFactorCodeAsync(string email, string username, string code, CancellationToken cancellationToken = default);
}
