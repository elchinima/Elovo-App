namespace Elovo.Application.DTOs;

public class UserPresenceDto
{
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
