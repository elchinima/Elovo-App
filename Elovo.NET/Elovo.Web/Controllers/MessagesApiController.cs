
namespace Elovo.Web.Controllers;

[Authorize]
[ApiController]
public class MessagesApiController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public MessagesApiController(IUserService userService, IHubContext<ChatHub> chatHubContext)
    {
        _userService = userService;
        _chatHubContext = chatHubContext;
    }

    [HttpGet("/api/conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        var profile = await _userService.GetProfileAsync(currentUserId, cancellationToken);
        var conversations = await _userService.GetConversationsAsync(currentUserId, cancellationToken);
        return Ok(new
        {
            activityVisibility = profile.ActivityVisibility,
            conversations
        });
    }

    [HttpGet("/api/users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var users = await _userService.SearchFriendCandidatesAsync(GetCurrentUserId(), query, cancellationToken);
        return Ok(users);
    }

    [HttpGet("/api/friend-requests")]
    public async Task<IActionResult> GetFriendRequests(CancellationToken cancellationToken)
    {
        var requests = await _userService.GetIncomingFriendRequestsAsync(GetCurrentUserId(), cancellationToken);
        return Ok(requests);
    }

    [HttpPost("/api/friend-requests")]
    public async Task<IActionResult> SendFriendRequest([FromBody] CreateFriendRequestDto dto, CancellationToken cancellationToken)
    {
        await _userService.SendFriendRequestAsync(GetCurrentUserId(), dto.ReceiverId, cancellationToken);
        return NoContent();
    }

    [HttpPost("/api/friend-requests/{requestId:guid}/accept")]
    public async Task<IActionResult> AcceptFriendRequest(Guid requestId, CancellationToken cancellationToken)
    {
        await _userService.AcceptFriendRequestAsync(GetCurrentUserId(), requestId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("/api/friends/{friendId:guid}")]
    public async Task<IActionResult> RemoveFriend(Guid friendId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        await _userService.RemoveFriendAsync(currentUserId, friendId, cancellationToken);

        await _chatHubContext.Clients.Group(UserGroup(currentUserId)).SendAsync("FriendRemoved", friendId, cancellationToken);
        await _chatHubContext.Clients.Group(UserGroup(friendId)).SendAsync("FriendRemoved", currentUserId, cancellationToken);

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Current user id is missing.");
    }

    private static string UserGroup(Guid userId) => $"user:{userId}";
}
