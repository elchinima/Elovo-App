
namespace Elovo.Web.Controllers;

[Authorize]
[Route("chat")]
public class ChatController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View();
    }
}
