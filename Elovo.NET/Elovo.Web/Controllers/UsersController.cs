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

        var userSession = await _context.UserSessions.FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);
        if (userSession is null)
        {
            var userExists = await _context.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
            if (!userExists)
            {
                return NotFound();
            }

            userSession = new UserSession { UserId = request.UserId };
            await _context.UserSessions.AddAsync(userSession, cancellationToken);
        }

        userSession.FcmToken = request.FcmToken.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
