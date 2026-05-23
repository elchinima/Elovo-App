
namespace Elovo.Application.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IUserService _userService;

    public ChatHub(IMessageService messageService, IUserService userService)
    {
        _messageService = messageService;
        _userService = userService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await _userService.SetOnlineStatusAsync(userId, true);
        await Clients.Others.SendAsync("UserOnline", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        await _userService.SetOnlineStatusAsync(userId, false);
        await Clients.Others.SendAsync("UserOffline", userId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(Guid receiverId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var senderId = GetCurrentUserId();
        var message = await _messageService.SendMessageAsync(senderId, new SendMessageDto
        {
            ReceiverId = receiverId,
            Content = content
        });

        await Clients.Groups(UserGroup(senderId), UserGroup(receiverId)).SendAsync("ReceiveMessage", message);
    }

    public async Task StartTyping(Guid receiverId)
    {
        await Clients.Group(UserGroup(receiverId)).SendAsync("UserTyping", GetCurrentUserId());
    }

    public async Task StopTyping(Guid receiverId)
    {
        await Clients.Group(UserGroup(receiverId)).SendAsync("UserStopTyping", GetCurrentUserId());
    }

    private Guid GetCurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new HubException("User identifier is missing.");
    }

    private static string UserGroup(Guid userId) => $"user:{userId}";
}
