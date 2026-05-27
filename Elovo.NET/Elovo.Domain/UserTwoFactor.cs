namespace Elovo.Domain;

public class UserTwoFactor
{
    public Guid UserId { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public string? TwoFactorCodeHash { get; set; }
    public DateTime? TwoFactorCodeExpiredAt { get; set; }

    public User User { get; set; } = null!;
}
