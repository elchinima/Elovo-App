namespace Elovo.Application.DTOs;

public class ConversationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsActivityHidden { get; set; }
    public bool IsLastSeenHidden { get; set; }
    public bool IsPremium { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public DateTime? OtherUserReadAt { get; set; }
    public int UnreadCount { get; set; }
    public string? ProfileImagePath { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ProfileImageSmallUrl { get; set; }
    public string Initial => string.IsNullOrWhiteSpace(Username) ? "?" : Username[..1].ToUpperInvariant();
}
