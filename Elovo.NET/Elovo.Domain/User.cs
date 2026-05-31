namespace Elovo.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ProfileImagePath { get; set; }
    public string? PreferredLanguage { get; set; } = "en";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserSession? Session { get; set; }
    public UserTwoFactor? TwoFactor { get; set; }
    public ICollection<Conversation> ConversationsAsFirstUser { get; set; } = new List<Conversation>();
    public ICollection<Conversation> ConversationsAsSecondUser { get; set; } = new List<Conversation>();
    public ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();
    public ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();
}
