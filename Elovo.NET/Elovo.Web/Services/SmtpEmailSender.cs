using System.Net;
using Resend;

namespace Elovo.Web.Services;

public class ResendEmailSender : IEmailSender
{
    private const string FromAddress = "onboarding@resend.dev";
    private readonly IResend _resend;

    public ResendEmailSender(IResend resend)
    {
        _resend = resend;
    }

    public async Task SendTwoFactorCodeAsync(string email, string username, string code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = new EmailMessage
        {
            From = FromAddress,
            Subject = "Your Elovo verification code",
            HtmlBody = BuildTwoFactorBody(username, code)
        };

        message.To.Add(email);

        await _resend.EmailSendAsync(message);
    }

    private static string BuildTwoFactorBody(string username, string code)
    {
        var safeUsername = WebUtility.HtmlEncode(username);
        var safeCode = WebUtility.HtmlEncode(code);

        return $"""
        <!doctype html>
        <html>
        <body style="margin:0;background:#070914;font-family:Inter,Segoe UI,Arial,sans-serif;color:#f5f7ff;">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#070914;padding:32px 12px;">
                <tr>
                    <td align="center">
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:520px;background:#121832;border:1px solid rgba(125,134,255,.22);border-radius:8px;overflow:hidden;">
                            <tr>
                                <td style="padding:28px 28px 12px;">
                                    <div style="font-size:12px;font-weight:800;letter-spacing:.14em;text-transform:uppercase;color:#4f8cff;">Elovo Messages</div>
                                    <h1 style="margin:14px 0 8px;font-size:28px;line-height:1.15;color:#f5f7ff;">Secure sign in</h1>
                                    <p style="margin:0;color:#aab0d4;line-height:1.6;">Hi {safeUsername}, enter this 7 digit code to finish logging in. It expires in 15 minutes.</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:18px 28px;">
                                    <div style="background:linear-gradient(135deg,#6d67ff,#4f8cff);border-radius:8px;padding:18px;text-align:center;font-size:34px;font-weight:900;letter-spacing:8px;color:#ffffff;">{safeCode}</div>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:0 28px 28px;color:#7780b8;font-size:13px;line-height:1.6;">If you did not try to sign in, change your password from your profile as soon as possible.</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
    }
}
