namespace Elovo.Application.DTOs;

public class AuthResultDto
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public string? Token { get; set; }
    public UserDto? User { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public Guid? TwoFactorUserId { get; set; }
    public string? TwoFactorEmail { get; set; }

    public static AuthResultDto Success(UserDto user, string token)
    {
        return new AuthResultDto
        {
            Succeeded = true,
            User = user,
            Token = token
        };
    }

    public static AuthResultDto Failure(string error)
    {
        return new AuthResultDto
        {
            Succeeded = false,
            Error = error
        };
    }

    public static AuthResultDto TwoFactorRequired(User user)
    {
        return new AuthResultDto
        {
            RequiresTwoFactor = true,
            TwoFactorUserId = user.Id,
            TwoFactorEmail = user.Email
        };
    }
}
