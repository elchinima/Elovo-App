namespace Elovo.Domain;

public class UserPremium
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}
