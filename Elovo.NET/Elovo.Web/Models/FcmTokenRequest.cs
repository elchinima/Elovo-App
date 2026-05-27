namespace Elovo.Web.Models;

public sealed class FcmTokenRequest
{
    public Guid UserId { get; set; }
    public string FcmToken { get; set; } = string.Empty;
}
