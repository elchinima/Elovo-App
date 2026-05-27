namespace Elovo.Web.Controllers;

[ApiController]
[AllowAnonymous]
public class UsersController : ControllerBase
{
    private readonly ElovoDbContext _context;

    public UsersController(ElovoDbContext context)
    {
        _context = context;
    }

    [HttpPost("/api/users/fcm-token")]
    public async Task<IActionResult> SaveFcmToken([FromBody] FcmTokenRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.FcmToken))
        {
            return BadRequest();
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.FcmToken = request.FcmToken.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
