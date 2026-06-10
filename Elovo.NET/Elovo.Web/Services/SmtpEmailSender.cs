using System.Net.Http;
using System.Text.Json;

namespace Elovo.Web.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public SmtpEmailSender(IConfiguration config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task SendTwoFactorCodeAsync(string email, string username, string code, string? language, CancellationToken cancellationToken = default)
    {
        await SendCodeEmailAsync(email, username, code, GetTwoFactorCopy(language), cancellationToken);
    }

    public async Task SendEmailConfirmationCodeAsync(string email, string username, string code, string? language, CancellationToken cancellationToken = default)
    {
        await SendCodeEmailAsync(email, username, code, GetEmailConfirmationCopy(language), cancellationToken);
    }

    private async Task SendCodeEmailAsync(string email, string username, string code, CodeEmailCopy copy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var apiKey = GetRequiredConfigurationValue("Email:ApiKey");
        var from = GetRequiredConfigurationValue("Email:From");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                sender = new { email = from },
                to = new[] { new { email } },
                subject = copy.Subject,
                htmlContent = BuildCodeBody(username, code, copy)
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Brevo email send failed: {(int)response.StatusCode} {responseText}");
        }
    }

    private static string BuildCodeBody(string username, string code, CodeEmailCopy copy)
    {
        var safeCode = System.Net.WebUtility.HtmlEncode(code);
        var safeTitle = System.Net.WebUtility.HtmlEncode(copy.Title);
        var safeGreeting = System.Net.WebUtility.HtmlEncode(string.Format(copy.Greeting, username));
        var safeWarning = System.Net.WebUtility.HtmlEncode(copy.Warning);

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

    private static CodeEmailCopy GetTwoFactorCopy(string? language)
    {
        return NormalizeLanguage(language) switch
        {
            "ru" => new CodeEmailCopy(
                "ru",
                "\u0412\u0430\u0448 \u043a\u043e\u0434 \u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u044f Elovo",
                "\u0411\u0435\u0437\u043e\u043f\u0430\u0441\u043d\u044b\u0439 \u0432\u0445\u043e\u0434",
                "\u0417\u0434\u0440\u0430\u0432\u0441\u0442\u0432\u0443\u0439\u0442\u0435, {0}. \u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u044d\u0442\u043e\u0442 7-\u0437\u043d\u0430\u0447\u043d\u044b\u0439 \u043a\u043e\u0434, \u0447\u0442\u043e\u0431\u044b \u0437\u0430\u0432\u0435\u0440\u0448\u0438\u0442\u044c \u0432\u0445\u043e\u0434. \u041a\u043e\u0434 \u0434\u0435\u0439\u0441\u0442\u0432\u0443\u0435\u0442 1 \u0447\u0430\u0441.",
                "\u0415\u0441\u043b\u0438 \u0432\u044b \u043d\u0435 \u043f\u044b\u0442\u0430\u043b\u0438\u0441\u044c \u0432\u043e\u0439\u0442\u0438, \u043a\u0430\u043a \u043c\u043e\u0436\u043d\u043e \u0441\u043a\u043e\u0440\u0435\u0435 \u0438\u0437\u043c\u0435\u043d\u0438\u0442\u0435 \u043f\u0430\u0440\u043e\u043b\u044c \u0432 \u043f\u0440\u043e\u0444\u0438\u043b\u0435."),
            "az" => new CodeEmailCopy(
                "az",
                "Elovo t\u0259sdiq kodunuz",
                "T\u0259hl\u00fck\u0259siz giri\u015f",
                "Salam, {0}. Giri\u015fi tamamlamaq \u00fc\u00e7\u00fcn bu 7 r\u0259q\u0259mli kodu daxil edin. Kod 1 saat q\u00fcvv\u0259d\u0259dir.",
                "Giri\u015f etm\u0259y\u0259 c\u0259hd etm\u0259misinizs\u0259, m\u00fcmk\u00fcn q\u0259d\u0259r tez profilinizd\u0259n parolunuzu d\u0259yi\u015fin."),
            _ => new CodeEmailCopy(
                "en",
                "Your Elovo verification code",
                "Secure sign in",
                "Hi {0}, enter this 7 digit code to finish logging in. It expires in 1 hour.",
                "If you did not try to sign in, change your password from your profile as soon as possible.")
        };
    }

    private static CodeEmailCopy GetEmailConfirmationCopy(string? language)
    {
        return NormalizeLanguage(language) switch
        {
            "ru" => new CodeEmailCopy(
                "ru",
                "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0434\u0438\u0442\u0435 \u043f\u043e\u0447\u0442\u0443 Elovo",
                "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u0435 \u043f\u043e\u0447\u0442\u044b",
                "\u0417\u0434\u0440\u0430\u0432\u0441\u0442\u0432\u0443\u0439\u0442\u0435, {0}. \u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u044d\u0442\u043e\u0442 7-\u0437\u043d\u0430\u0447\u043d\u044b\u0439 \u043a\u043e\u0434, \u0447\u0442\u043e\u0431\u044b \u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0434\u0438\u0442\u044c \u043f\u043e\u0447\u0442\u0443. \u041a\u043e\u0434 \u0434\u0435\u0439\u0441\u0442\u0432\u0443\u0435\u0442 1 \u0447\u0430\u0441.",
                "\u0415\u0441\u043b\u0438 \u0432\u044b \u043d\u0435 \u0434\u043e\u0431\u0430\u0432\u043b\u044f\u043b\u0438 \u044d\u0442\u0443 \u043f\u043e\u0447\u0442\u0443, \u0441\u043c\u0435\u043d\u0438\u0442\u0435 \u043f\u0430\u0440\u043e\u043b\u044c \u0432 \u043f\u0440\u043e\u0444\u0438\u043b\u0435."),
            "az" => new CodeEmailCopy(
                "az",
                "Elovo e-po\u00e7tunuzu t\u0259sdiql\u0259yin",
                "E-po\u00e7t t\u0259sdiqi",
                "Salam, {0}. E-po\u00e7tunuzu t\u0259sdiql\u0259m\u0259k \u00fc\u00e7\u00fcn bu 7 r\u0259q\u0259mli kodu daxil edin. Kod 1 saat q\u00fcvv\u0259d\u0259dir.",
                "Bu e-po\u00e7tu siz \u0259lav\u0259 etm\u0259misinizs\u0259, profilinizd\u0259 parolunuzu d\u0259yi\u015fin."),
            _ => new CodeEmailCopy(
                "en",
                "Confirm your Elovo email",
                "Email verification",
                "Hi {0}, enter this 7 digit code to verify your email. It expires in 1 hour.",
                "If you did not add this email, change your password from your profile.")
        };
    }

    private static string NormalizeLanguage(string? language)
    {
        var normalized = (language ?? string.Empty).Trim().ToLowerInvariant().Split('-')[0];
        return normalized is "ru" or "az" ? normalized : "en";
    }

    private sealed record CodeEmailCopy(string Language, string Subject, string Title, string Greeting, string Warning);

    private string GetRequiredConfigurationValue(string key)
    {
        var value = _config[key];
        return string.IsNullOrWhiteSpace(value) || value.StartsWith("Set via ", StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidOperationException($"{key} is not configured.")
            : value;
    }
}
