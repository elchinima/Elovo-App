using System.Net;
using System.Net.Mail;

namespace Elovo.Web.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendTwoFactorCodeAsync(string email, string username, string code, string? language, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var host = GetRequiredConfigurationValue("Email:SmtpHost");
        var port = int.Parse(GetRequiredConfigurationValue("Email:SmtpPort"));
        var smtpUsername = GetRequiredConfigurationValue("Email:SmtpUsername");
        var smtpPassword = GetRequiredConfigurationValue("Email:SmtpPassword");
        var from = GetRequiredConfigurationValue("Email:From");
        var enableSsl = bool.Parse(GetRequiredConfigurationValue("Email:EnableSsl"));

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            EnableSsl = enableSsl
        };

        using var message = new MailMessage
        {
            From = new MailAddress(from),
            Subject = GetTwoFactorCopy(language).Subject,
            Body = BuildTwoFactorBody(username, code, language),
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(email));

        await client.SendMailAsync(message, CancellationToken.None);
    }

    private static string BuildTwoFactorBody(string username, string code, string? language)
    {
        var copy = GetTwoFactorCopy(language);
        var safeCode = WebUtility.HtmlEncode(code);
        var safeTitle = WebUtility.HtmlEncode(copy.Title);
        var safeGreeting = WebUtility.HtmlEncode(string.Format(copy.Greeting, username));
        var safeWarning = WebUtility.HtmlEncode(copy.Warning);

        return $"""
        <!doctype html>
        <html lang="{copy.Language}">
        <body style="margin:0;background:#070914;font-family:Inter,Segoe UI,Arial,sans-serif;color:#f5f7ff;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#070914;padding:32px 12px;">
                <tr>
                    <td align="center">
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:520px;background:#121832;border:1px solid rgba(125,134,255,.22);border-radius:8px;overflow:hidden;">
                            <tr>
                                <td style="padding:28px 28px 12px;">
                                    <div style="font-size:12px;font-weight:800;letter-spacing:.14em;text-transform:uppercase;color:#4f8cff;">Elovo Messages</div>
                                    <h1 style="margin:14px 0 8px;font-size:28px;line-height:1.15;color:#f5f7ff;">{safeTitle}</h1>
                                    <p style="margin:0;color:#aab0d4;line-height:1.6;">{safeGreeting}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:18px 28px;">
                                    <div style="background:linear-gradient(135deg,#6d67ff,#4f8cff);border-radius:8px;padding:18px;text-align:center;font-size:34px;font-weight:900;letter-spacing:8px;color:#ffffff;">{safeCode}</div>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:0 28px 28px;color:#7780b8;font-size:13px;line-height:1.6;">{safeWarning}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
    }

    private static TwoFactorEmailCopy GetTwoFactorCopy(string? language)
    {
        return NormalizeLanguage(language) switch
        {
            "ru" => new TwoFactorEmailCopy(
                "ru",
                "Ваш код подтверждения Elovo",
                "Безопасный вход",
                "Здравствуйте, {0}. Введите этот 7-значный код, чтобы завершить вход. Код действует 15 минут.",
                "Если вы не пытались войти, как можно скорее измените пароль в профиле."),
            "az" => new TwoFactorEmailCopy(
                "az",
                "Elovo təsdiq kodunuz",
                "Təhlükəsiz giriş",
                "Salam, {0}. Girişi tamamlamaq üçün bu 7 rəqəmli kodu daxil edin. Kod 15 dəqiqə qüvvədədir.",
                "Giriş etməyə cəhd etməmisinizsə, mümkün qədər tez profilinizdən parolunuzu dəyişin."),
            _ => new TwoFactorEmailCopy(
                "en",
                "Your Elovo verification code",
                "Secure sign in",
                "Hi {0}, enter this 7 digit code to finish logging in. It expires in 15 minutes.",
                "If you did not try to sign in, change your password from your profile as soon as possible.")
        };
    }

    private static string NormalizeLanguage(string? language)
    {
        var normalized = (language ?? string.Empty).Trim().ToLowerInvariant().Split('-')[0];
        return normalized is "ru" or "az" ? normalized : "en";
    }

    private sealed record TwoFactorEmailCopy(string Language, string Subject, string Title, string Greeting, string Warning);

    private string GetRequiredConfigurationValue(string key)
    {
        var value = _config[key];
        return string.IsNullOrWhiteSpace(value) || value.StartsWith("Set via ", StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidOperationException($"{key} is not configured.")
            : value;
    }
}
