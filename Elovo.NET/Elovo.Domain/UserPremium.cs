namespace Elovo.Domain;

public class UserPremium
{
    public Guid UserId { get; set; }
    public bool IsExtendedVoiceMessagesEnabled { get; set; }
    public bool IsRawImageUploadsEnabled { get; set; }
    public bool IsVideoUploadsEnabled { get; set; }
    public bool IsPremiumBadgeVisible { get; set; } = true;

    public User User { get; set; } = null!;
}
