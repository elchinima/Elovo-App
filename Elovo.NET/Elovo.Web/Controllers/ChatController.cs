
namespace Elovo.Web.Controllers;

[Authorize]
[Route("chat")]
public class ChatController : Controller
{
    private const string AuthCookieName = "ElovoAuthToken";
    private readonly IUserService _userService;

    public ChatController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            Response.Cookies.Delete(AuthCookieName);
            return RedirectToAction("Login", "Auth");
        }

        ProfileDto profile;
        try
        {
            profile = await _userService.GetProfileAsync(userId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            Response.Cookies.Delete(AuthCookieName);
            return RedirectToAction("Login", "Auth");
        }

        ViewBag.CurrentUserId = userId;
        ViewBag.CurrentUserInitial = profile.Initial;
        ViewBag.CurrentUserProfileImageUrl = profile.ProfileImageUrl;
        ViewBag.CurrentUserActivityVisibility = profile.ActivityVisibility;
        ViewBag.CurrentUserIsPremium = profile.IsPremium;
        ViewBag.CurrentUserHasExtendedVoiceMessages = profile.IsExtendedVoiceMessagesEnabled;
        ViewBag.CurrentUserHasRawImageUploads = profile.IsRawImageUploadsEnabled;
        ViewBag.CurrentUserHasVideoUploads = profile.IsVideoUploadsEnabled;
        return View();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
