namespace Elovo.Application.DTOs;

public class ProfileDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public DateTime? EmailCooldownEndsAt { get; set; }
    public string? ProfileImagePath { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ProfileImageSmallUrl { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public bool IsPremium { get; set; }
    public bool IsExtendedVoiceMessagesEnabled { get; set; }
    public bool IsRawImageUploadsEnabled { get; set; }
    public bool IsVideoUploadsEnabled { get; set; }
    public bool IsPremiumBadgeVisible { get; set; }
    public string ActivityVisibility { get; set; } = "full";
    public string Initial => string.IsNullOrWhiteSpace(Username) ? "?" : Username[..1].ToUpperInvariant();
}
