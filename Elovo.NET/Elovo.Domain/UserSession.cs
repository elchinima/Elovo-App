namespace Elovo.Domain;

public class UserSession
{
    public Guid UserId { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
    public string? LastLoginIp { get; set; }
    public string? RegistrationIp { get; set; }
    public string? FcmToken { get; set; }
    public string? PreferredLanguage { get; set; } = "en";
    public string ActivityVisibility { get; set; } = "full";

    public User User { get; set; } = null!;
}
