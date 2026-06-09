namespace Elovo.Application.Services;

public interface IEmailSender
{
    Task SendTwoFactorCodeAsync(string email, string username, string code, string? language, CancellationToken cancellationToken = default);
    Task SendEmailConfirmationCodeAsync(string email, string username, string code, string? language, CancellationToken cancellationToken = default);
}
