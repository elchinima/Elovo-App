
namespace Elovo.Application.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, int> ConnectedUsers = new();

    private readonly IMessageService _messageService;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;

    public ChatHub(IMessageService messageService, IUserService userService, IUnitOfWork unitOfWork)
    {
        _messageService = messageService;
        _userService = userService;
        _unitOfWork = unitOfWork;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        ConnectedUsers.AddOrUpdate(userId, 1, (_, count) => count + 1);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await _userService.SetOnlineStatusAsync(userId, true);
        await SendPendingMessagesAsync(userId);
        await Clients.Others.SendAsync("UserOnline", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        var isOffline = ConnectedUsers.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1)) == 0;
        if (isOffline)
        {
            ConnectedUsers.TryRemove(userId, out _);
            await _userService.SetOnlineStatusAsync(userId, false);
            await Clients.Others.SendAsync("UserOffline", userId);
        }

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

        if (IsConnected(receiverId))
        {
            await Clients.Groups(UserGroup(senderId), UserGroup(receiverId)).SendAsync("ReceiveMessage", message);
            return;
        }

        await _unitOfWork.PendingMessages.AddAsync(new PendingMessage
        {
            Id = message.Id,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            SentAt = message.SentAt,
            IsVoice = message.IsVoice,
            VoiceUrl = message.VoiceUrl
        });

        await _unitOfWork.SaveChangesAsync();
        await Clients.Group(UserGroup(senderId)).SendAsync("ReceiveMessage", message);
    }

    public async Task SendImageMessage(Guid receiverId, string imagePath, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        var senderId = GetCurrentUserId();
        var message = await _messageService.SendImageMessageAsync(senderId, new SendMessageDto
        {
            ReceiverId = receiverId,
            Content = "Image",
            ImagePath = imagePath,
            ImageFileName = fileName
        });

        if (IsConnected(receiverId))
        {
            await Clients.Groups(UserGroup(senderId), UserGroup(receiverId)).SendAsync("ReceiveMessage", message);
            return;
        }

        await _unitOfWork.PendingMessages.AddAsync(new PendingMessage
        {
            Id = message.Id,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            SentAt = message.SentAt,
            IsVoice = message.IsVoice,
            VoiceUrl = message.ImageFileName
        });

        await _unitOfWork.SaveChangesAsync();
        await Clients.Group(UserGroup(senderId)).SendAsync("ReceiveMessage", message);
    }

    public async Task StartTyping(Guid receiverId)
    {
        await Clients.Group(UserGroup(receiverId)).SendAsync("UserTyping", GetCurrentUserId());
    }

    public async Task StopTyping(Guid receiverId)
    {
        await Clients.Group(UserGroup(receiverId)).SendAsync("UserStopTyping", GetCurrentUserId());
    }

    public async Task MarkMessagesRead(Guid senderId)
    {
        var readerId = GetCurrentUserId();
        await Clients.Group(UserGroup(senderId)).SendAsync("MessagesRead", readerId, DateTime.UtcNow);
    }

    private Guid GetCurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new HubException("User identifier is missing.");
    }

    private static string UserGroup(Guid userId) => $"user:{userId}";

    private static bool IsConnected(Guid userId)
    {
        return ConnectedUsers.TryGetValue(userId, out var count) && count > 0;
    }

    private static bool IsImagePath(string value)
    {
        return value.StartsWith("/uploads/chat-images/", StringComparison.Ordinal);
    }

    private async Task SendPendingMessagesAsync(Guid userId)
    {
        var messages = await _unitOfWork.PendingMessages.GetByReceiverIdAsync(userId, Context.ConnectionAborted);
        foreach (var message in messages)
        {
            await Clients.Group(UserGroup(userId)).SendAsync("ReceiveMessage", new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = IsImagePath(message.Content) ? "Image" : message.Content,
                SentAt = message.SentAt,
                IsVoice = message.IsVoice,
                VoiceUrl = message.VoiceUrl,
                IsImage = IsImagePath(message.Content),
                ImagePath = IsImagePath(message.Content) ? message.Content : null,
                ImageFileName = IsImagePath(message.Content) ? message.VoiceUrl : null
            }, Context.ConnectionAborted);
        }

        await _unitOfWork.PendingMessages.DeleteRangeAsync(messages, Context.ConnectionAborted);
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
    }
}
