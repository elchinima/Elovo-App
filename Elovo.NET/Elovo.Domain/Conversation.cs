namespace Elovo.Domain;

public class Conversation
{
    public Guid Id { get; set; }
    public Guid FirstUserId { get; set; }
    public Guid SecondUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User FirstUser { get; set; } = null!;
    public User SecondUser { get; set; } = null!;
}
