
namespace Elovo.Application.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, int> ConnectedUsers = new();

    private readonly IMessageService _messageService;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageStorageService _imageStorageService;

    public ChatHub(IMessageService messageService, IUserService userService, IUnitOfWork unitOfWork, IImageStorageService imageStorageService)
    {
        _messageService = messageService;
        _userService = userService;
        _unitOfWork = unitOfWork;
        _imageStorageService = imageStorageService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        ConnectedUsers.AddOrUpdate(userId, 1, (_, count) => count + 1);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        var lastSeenAt = await _userService.SetOnlineStatusAsync(userId, true, ClientIpAddressResolver.Resolve(Context.GetHttpContext()));
        await SendPendingMessagesAsync(userId);
        await Clients.Others.SendAsync("UserOnline", userId, lastSeenAt);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        var isOffline = ConnectedUsers.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1)) == 0;
        if (isOffline)
        {
            ConnectedUsers.TryRemove(userId, out _);
            var lastSeenAt = await _userService.SetOnlineStatusAsync(userId, false);
            await Clients.Others.SendAsync("UserOffline", userId, lastSeenAt);
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
            await Clients.Groups(UserGroup(senderId), UserGroup(receiverId)).SendAsync("ReceiveMessage", ToClientMessage(message));
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
        message.IsPending = true;
        await Clients.Group(UserGroup(senderId)).SendAsync("ReceiveMessage", ToClientMessage(message));
    }

    public async Task SendImageMessage(Guid receiverId, string imagePath, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !_imageStorageService.IsImagePath(imagePath))
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
            await Clients.Groups(UserGroup(senderId), UserGroup(receiverId)).SendAsync("ReceiveMessage", ToClientMessage(message));
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
        message.IsPending = true;
        await Clients.Group(UserGroup(senderId)).SendAsync("ReceiveMessage", ToClientMessage(message));
    }

    public async Task<bool> DeletePendingMessage(Guid messageId)
    {
        var senderId = GetCurrentUserId();
        var message = await _unitOfWork.PendingMessages.GetByIdAsync(messageId, Context.ConnectionAborted);
        if (message is null || message.SenderId != senderId)
        {
            return false;
        }

        if (_imageStorageService.IsImagePath(message.Content))
        {
            await _imageStorageService.DeleteAsync(message.Content, Context.ConnectionAborted);
        }

        await _unitOfWork.PendingMessages.DeleteAsync(message, Context.ConnectionAborted);
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
        await Clients.Group(UserGroup(senderId)).SendAsync("PendingMessageDeleted", message.ReceiverId, message.Id, Context.ConnectionAborted);
        return true;
    }

    public async Task<bool> EditPendingMessage(Guid messageId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var senderId = GetCurrentUserId();
        var message = await _unitOfWork.PendingMessages.GetByIdAsync(messageId, Context.ConnectionAborted);
        if (message is null || message.SenderId != senderId || _imageStorageService.IsImagePath(message.Content))
        {
            return false;
        }

        message.Content = content.Trim();
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
        await Clients.Group(UserGroup(senderId)).SendAsync("PendingMessageEdited", message.ReceiverId, message.Id, message.Content, Context.ConnectionAborted);
        return true;
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

    private async Task SendPendingMessagesAsync(Guid userId)
    {
        var messages = await _unitOfWork.PendingMessages.GetByReceiverIdAsync(userId, Context.ConnectionAborted);
        var deliveredMessageIdsBySender = new Dictionary<Guid, List<Guid>>();

        foreach (var message in messages)
        {
            var isImage = _imageStorageService.IsImagePath(message.Content);
            await Clients.Group(UserGroup(userId)).SendAsync("ReceiveMessage", new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = isImage ? "Image" : message.Content,
                SentAt = message.SentAt,
                IsVoice = message.IsVoice,
                VoiceUrl = message.VoiceUrl,
                IsImage = isImage,
                ImagePath = isImage ? _imageStorageService.GetPublicUrl(message.Content) : null,
                ImageStoragePath = isImage ? message.Content : null,
                ImageFileName = isImage ? message.VoiceUrl : null
            }, Context.ConnectionAborted);

            if (!deliveredMessageIdsBySender.TryGetValue(message.SenderId, out var deliveredMessageIds))
            {
                deliveredMessageIds = [];
                deliveredMessageIdsBySender[message.SenderId] = deliveredMessageIds;
            }

            deliveredMessageIds.Add(message.Id);
        }

        await _unitOfWork.PendingMessages.DeleteRangeAsync(messages, Context.ConnectionAborted);
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);

        var deliveredAt = DateTime.UtcNow;
        foreach (var item in deliveredMessageIdsBySender)
        {
            await Clients.Group(UserGroup(item.Key)).SendAsync("MessagesDelivered", userId, item.Value, deliveredAt, Context.ConnectionAborted);
        }
    }

    private MessageDto ToClientMessage(MessageDto message)
    {
        if (!message.IsImage || string.IsNullOrWhiteSpace(message.ImagePath))
        {
            return message;
        }

        return new MessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            SentAt = message.SentAt,
            ReadAt = message.ReadAt,
            IsVoice = message.IsVoice,
            VoiceUrl = message.VoiceUrl,
            IsImage = true,
            ImagePath = _imageStorageService.GetPublicUrl(message.ImagePath),
            ImageStoragePath = message.ImageStoragePath ?? message.ImagePath,
            IsPending = message.IsPending,
            ImageFileName = message.ImageFileName
        };
    }
}
