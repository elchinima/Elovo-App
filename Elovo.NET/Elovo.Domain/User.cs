namespace Elovo.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? ProfileImagePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserSession? Session { get; set; }
    public UserTwoFactor? TwoFactor { get; set; }
    public UserEmail? EmailSettings { get; set; }
    public UserPremium? Premium { get; set; }
    public ICollection<Conversation> ConversationsAsFirstUser { get; set; } = new List<Conversation>();
    public ICollection<Conversation> ConversationsAsSecondUser { get; set; } = new List<Conversation>();
    public ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();
    public ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();
}
