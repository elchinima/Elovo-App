namespace Elovo.Application.DTOs;

public class RegisterDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
}
