namespace Elovo.Application.DTOs;

public class FriendCandidateDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsActivityHidden { get; set; }
    public string Status { get; set; } = "none";
    public string? ProfileImagePath { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string Initial => string.IsNullOrWhiteSpace(Username) ? "?" : Username[..1].ToUpperInvariant();
}
