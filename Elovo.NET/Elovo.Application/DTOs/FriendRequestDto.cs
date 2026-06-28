namespace Elovo.Application.DTOs;

public class FriendRequestDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ProfileImagePath { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ProfileImageSmallUrl { get; set; }
    public string Initial => string.IsNullOrWhiteSpace(SenderUsername) ? "?" : SenderUsername[..1].ToUpperInvariant();
}
