
namespace Elovo.Web.Controllers;

[Authorize]
[Route("chat")]
public class ChatController : Controller
{
    private readonly IUserService _userService;

    public ChatController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var profile = await _userService.GetProfileAsync(userId, cancellationToken);
        ViewBag.CurrentUserId = userId;
        ViewBag.CurrentUserInitial = profile.Initial;
        ViewBag.CurrentUserProfileImageUrl = profile.ProfileImageUrl;
        return View();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Current user id is missing.");
    }
}
