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
    public bool IsTwoFactorEnabled { get; set; }
    public string Initial => string.IsNullOrWhiteSpace(Username) ? "?" : Username[..1].ToUpperInvariant();
}
