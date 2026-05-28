
namespace Elovo.Application.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageStorageService _imageStorageService;
    private readonly IUserPresenceTracker _presenceTracker;

    public ChatHub(
        IMessageService messageService,
        IUserService userService,
        IUnitOfWork unitOfWork,
        IImageStorageService imageStorageService,
        IUserPresenceTracker presenceTracker)
    {
        _messageService = messageService;
        _userService = userService;
        _unitOfWork = unitOfWork;
        _imageStorageService = imageStorageService;
        _presenceTracker = presenceTracker;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        var becameOnline = _presenceTracker.Connect(userId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        var lastSeenAt = becameOnline
            ? await _userService.SetOnlineStatusAsync(userId, true, ClientIpAddressResolver.Resolve(Context.GetHttpContext()))
            : null;
        await SendPendingMessagesAsync(userId);
        if (becameOnline)
        {
            await Clients.Others.SendAsync("UserOnline", userId, lastSeenAt);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        var isOffline = _presenceTracker.Disconnect(userId, Context.ConnectionId);
        if (isOffline)
        {
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
        await Clients.Groups(UserGroup(senderId), UserGroup(receiverId)).SendAsync("ReceiveMessage", ToClientMessage(message));
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

        await _unitOfWork.PendingMessages.AddAsync(new PendingMessage
        {
            Id = message.Id,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.ImageStoragePath ?? message.ImagePath ?? message.Content,
            SentAt = message.SentAt,
            IsVoice = message.IsVoice,
            VoiceUrl = message.ImageFileName
        });

        await _unitOfWork.SaveChangesAsync();
        message.IsPending = true;
        await Clients.Group(UserGroup(senderId)).SendAsync("ReceiveMessage", ToClientMessage(message));
        await Clients.Group(UserGroup(receiverId)).SendAsync("ReceiveMessage", ToClientMessage(message));
    }

    public async Task<bool> DeletePendingMessage(Guid messageId)
    {
        var senderId = GetCurrentUserId();
        var message = await _unitOfWork.PendingMessages.GetByIdAsync(messageId, Context.ConnectionAborted);
        if (message is null || message.SenderId != senderId)
        {
            return false;
        }

        await DeletePendingMessageImageAsync(message);

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
        var conversation = await _unitOfWork.Conversations.GetBetweenUsersAsync(readerId, senderId, Context.ConnectionAborted);
        if (conversation is null)
        {
            return;
        }

        var readAt = DateTime.UtcNow;
        if (conversation.FirstUserId == readerId)
        {
            conversation.FirstUserReadAt = readAt;
        }
        else
        {
            conversation.SecondUserReadAt = readAt;
        }

        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
        await Clients.Group(UserGroup(senderId)).SendAsync("MessagesRead", readerId, readAt);
    }

    public async Task AcknowledgePendingMessages(Guid[] messageIds)
    {
        if (messageIds.Length == 0)
        {
            return;
        }

        var receiverId = GetCurrentUserId();
        var messages = new List<PendingMessage>();
        foreach (var messageId in messageIds.Distinct().Take(100))
        {
            var message = await _unitOfWork.PendingMessages.GetByIdAsync(messageId, Context.ConnectionAborted);
            if (message is not null && message.ReceiverId == receiverId)
            {
                messages.Add(message);
            }
        }

        if (messages.Count == 0)
        {
            return;
        }

        var deliveredMessageIdsBySender = messages
            .GroupBy(message => message.SenderId)
            .ToDictionary(group => group.Key, group => group.Select(message => message.Id).ToList());

        foreach (var message in messages)
        {
            await DeletePendingMessageImageAsync(message);
        }

        await _unitOfWork.PendingMessages.DeleteRangeAsync(messages, Context.ConnectionAborted);
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);

        var deliveredAt = DateTime.UtcNow;
        foreach (var item in deliveredMessageIdsBySender)
        {
            await Clients.Group(UserGroup(item.Key)).SendAsync("MessagesDelivered", receiverId, item.Value, deliveredAt, Context.ConnectionAborted);
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new HubException("User identifier is missing.");
    }

    private static string UserGroup(Guid userId) => $"user:{userId}";

    private async Task SendPendingMessagesAsync(Guid userId)
    {
        var messages = await _unitOfWork.PendingMessages.GetByReceiverIdAsync(userId, Context.ConnectionAborted);

        foreach (var message in messages)
        {
            var isImage = _imageStorageService.IsImagePath(message.Content);
            await Clients.Caller.SendAsync("ReceiveMessage", new MessageDto
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
                ImageFileName = isImage ? message.VoiceUrl : null,
                IsPending = true
            }, Context.ConnectionAborted);
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

    private async Task DeletePendingMessageImageAsync(PendingMessage message)
    {
        if (_imageStorageService.IsImagePath(message.Content))
        {
            await _imageStorageService.DeleteAsync(message.Content, Context.ConnectionAborted);
        }
    }
}
