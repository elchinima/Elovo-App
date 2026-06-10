namespace Elovo.Domain;

public class UserEmail
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public string? EmailConfirmationCodeHash { get; set; }
    public DateTime? EmailConfirmationCodeExpiredAt { get; set; }
    public DateTime? LastEmailSentAt { get; set; }

    public User User { get; set; } = null!;
}
