namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public sealed class AccountController : ControllerBase
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "ru",
        "az"
    };

    private readonly ElovoDbContext _context;

    public AccountController(ElovoDbContext context)
    {
        _context = context;
    }

    [HttpPatch("/api/account/language")]
    public async Task<IActionResult> UpdateLanguage([FromBody] LanguagePreferenceRequest request, CancellationToken cancellationToken)
    {
        var language = request.Language?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(language) || !SupportedLanguages.Contains(language))
        {
            return BadRequest("Language is invalid.");
        }

        var userId = GetCurrentUserId();
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.PreferredLanguage = language;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Current user id is missing.");
    }
}
