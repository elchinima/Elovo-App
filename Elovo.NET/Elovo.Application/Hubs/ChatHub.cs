
namespace Elovo.Application.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private const double StandardVoiceMessageLimitSeconds = 60.5;
    private const double ExtendedVoiceMessageLimitSeconds = 180.5;
    private readonly IMessageService _messageService;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageStorageService _imageStorageService;
    private readonly IUserPresenceTracker _presenceTracker;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ICallHistoryService _callHistoryService;

    public ChatHub(
        IMessageService messageService,
        IUserService userService,
        IUnitOfWork unitOfWork,
        IImageStorageService imageStorageService,
        IUserPresenceTracker presenceTracker,
        IPushNotificationService pushNotificationService,
        ICallHistoryService callHistoryService)
    {
        _messageService = messageService;
        _userService = userService;
        _unitOfWork = unitOfWork;
        _imageStorageService = imageStorageService;
        _presenceTracker = presenceTracker;
        _pushNotificationService = pushNotificationService;
        _callHistoryService = callHistoryService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        var becameOnline = _presenceTracker.Connect(userId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        var presence = becameOnline
            ? await _userService.SetOnlineStatusAsync(userId, true, ClientIpAddressResolver.Resolve(Context.GetHttpContext()))
            : null;
        await SendPendingMessagesAsync(userId);
        await SendActiveCallAsync(userId);
        if (becameOnline)
        {
            await NotifyPresenceChangedAsync(userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        var isOffline = _presenceTracker.Disconnect(userId, Context.ConnectionId);
        if (isOffline)
        {
            await _userService.SetOnlineStatusAsync(userId, false);
            await NotifyPresenceChangedAsync(userId);
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

    public async Task SendVoiceMessage(Guid receiverId, string voicePath, double durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(voicePath) ||
            !_imageStorageService.IsVoicePath(voicePath) ||
            durationSeconds <= 0)
        {
            return;
        }

        var senderId = GetCurrentUserId();
        var sender = await _userService.GetProfileAsync(senderId, Context.ConnectionAborted);
        var maxDurationSeconds = sender.IsExtendedVoiceMessagesEnabled
            ? ExtendedVoiceMessageLimitSeconds
            : StandardVoiceMessageLimitSeconds;
        if (durationSeconds > maxDurationSeconds)
        {
            return;
        }

        var message = await _messageService.SendVoiceMessageAsync(senderId, new SendMessageDto
        {
            ReceiverId = receiverId,
            Content = "Voice message",
            VoicePath = voicePath,
            VoiceDurationSeconds = durationSeconds
        });

        await _unitOfWork.PendingMessages.AddAsync(new PendingMessage
        {
            Id = message.Id,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            SentAt = message.SentAt,
            IsVoice = true,
            VoiceUrl = message.VoiceUrl
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

        await DeletePendingMessageMediaAsync(message);

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
        if (message is null ||
            message.SenderId != senderId ||
            _imageStorageService.IsImagePath(message.Content) ||
            message.IsVoice ||
            message.IsCall)
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
            await DeletePendingMessageMediaAsync(message);
        }

        await _unitOfWork.PendingMessages.DeleteRangeAsync(messages, Context.ConnectionAborted);
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);

        var deliveredAt = DateTime.UtcNow;
        foreach (var item in deliveredMessageIdsBySender)
        {
            await Clients.Group(UserGroup(item.Key)).SendAsync("MessagesDelivered", receiverId, item.Value, deliveredAt, Context.ConnectionAborted);
        }
    }

    public async Task CallUser(string targetUserId)
    {
        var callerId = GetCurrentUserId();
        var targetId = ParseUserId(targetUserId);
        var caller = await _userService.GetProfileAsync(callerId, Context.ConnectionAborted);
        var activeCall = await _unitOfWork.ActiveCalls.GetByParticipantsAsync(callerId, targetId, Context.ConnectionAborted);

        if (activeCall is not null && activeCall.IsRejected)
        {
            await _unitOfWork.ActiveCalls.DeleteAsync(activeCall, Context.ConnectionAborted);
            await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
            activeCall = null;
        }

        if (activeCall is not null && !string.IsNullOrWhiteSpace(activeCall.OfferSdp))
        {
            return;
        }

        if (activeCall is null)
        {
            activeCall = new ActiveCall
            {
                Id = Guid.NewGuid(),
                CallerId = callerId,
                ReceiverId = targetId
            };
            await _unitOfWork.ActiveCalls.AddAsync(activeCall, Context.ConnectionAborted);
        }

        activeCall.CallerName = caller.Username;
        activeCall.CallerAvatar = caller.ProfileImageUrl ?? string.Empty;
        activeCall.OfferSdp = null;
        activeCall.IsRejected = false;
        activeCall.AnsweredAt = null;
        activeCall.StartedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);

        if (!_presenceTracker.IsOnline(targetId))
        {
            var fcmToken = await _unitOfWork.Users.GetFcmTokenByUserIdAsync(targetId, Context.ConnectionAborted);
            if (!string.IsNullOrWhiteSpace(fcmToken))
            {
                await _pushNotificationService.SendCallPushAsync(
                    fcmToken,
                    caller.Username,
                    caller.ProfileImageUrl ?? string.Empty,
                    callerId.ToString());
            }
        }

        await Clients.Group(UserGroup(targetId)).SendAsync(
            "IncomingCall",
            callerId,
            caller.Username,
            caller.ProfileImageUrl,
            Context.ConnectionAborted);
    }

    public async Task CallOffer(string targetUserId, string sdpOffer)
    {
        if (string.IsNullOrWhiteSpace(sdpOffer))
        {
            return;
        }

        var callerId = GetCurrentUserId();
        var targetId = ParseUserId(targetUserId);
        var activeCall = await _unitOfWork.ActiveCalls.GetByParticipantsAsync(callerId, targetId, Context.ConnectionAborted);
        if (activeCall is null || activeCall.IsRejected)
        {
            return;
        }

        activeCall.OfferSdp = sdpOffer;
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);

        await Clients.Group(UserGroup(targetId)).SendAsync("CallOffer", sdpOffer, callerId, Context.ConnectionAborted);
    }

    public async Task RequestCallOffer(string callerId)
    {
        var currentUserId = GetCurrentUserId();
        await Clients.Group(UserGroup(ParseUserId(callerId)))
            .SendAsync("ResendCallOffer", currentUserId.ToString());
    }

    public async Task CallAnswer(string callerId, string sdpAnswer)
    {
        if (string.IsNullOrWhiteSpace(sdpAnswer))
        {
            return;
        }

        var parsedCallerId = ParseUserId(callerId);
        var activeCall = await _unitOfWork.ActiveCalls.GetByParticipantsAsync(parsedCallerId, GetCurrentUserId(), Context.ConnectionAborted);
        if (activeCall is null)
        {
            return;
        }

        activeCall.AnsweredAt ??= DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(Context.ConnectionAborted);
        await Clients.Group(UserGroup(parsedCallerId)).SendAsync("CallAnswered", sdpAnswer, Context.ConnectionAborted);
    }

    public async Task IceCandidate(string targetUserId, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        await Clients.Group(UserGroup(ParseUserId(targetUserId))).SendAsync("IceCandidate", candidate, Context.ConnectionAborted);
    }

    public async Task CallReject(string callerId)
    {
        var parsedCallerId = ParseUserId(callerId);
        var activeCall = await _unitOfWork.ActiveCalls.GetByParticipantsAsync(parsedCallerId, GetCurrentUserId(), Context.ConnectionAborted);
        if (activeCall is not null)
        {
            activeCall.IsRejected = true;
            var message = await _callHistoryService.CompleteAsync(activeCall, CallStatuses.Rejected, Context.ConnectionAborted);
            await PublishCallHistoryAsync(message);
        }

        await Clients.Group(UserGroup(parsedCallerId)).SendAsync("CallRejected");
    }

    public async Task CallEnd(string targetUserId)
    {
        var currentUserId = GetCurrentUserId();
        var targetId = ParseUserId(targetUserId);
        var activeCalls = await _unitOfWork.ActiveCalls.GetBetweenUsersAsync(currentUserId, targetId, Context.ConnectionAborted);
        var activeCall = activeCalls.OrderByDescending(x => x.StartedAt).FirstOrDefault();
        if (activeCall is not null)
        {
            var status = activeCall.AnsweredAt.HasValue
                ? CallStatuses.Answered
                : activeCall.CallerId == currentUserId ? CallStatuses.Missed : CallStatuses.Rejected;
            var message = await _callHistoryService.CompleteAsync(activeCall, status, Context.ConnectionAborted);
            await PublishCallHistoryAsync(message);
        }

        await Clients.Group(UserGroup(targetId)).SendAsync("CallEnded", Context.ConnectionAborted);
    }

    public async Task CancelCall(string targetUserId)
    {
        var callerId = GetCurrentUserId();
        var targetId = ParseUserId(targetUserId);
        var activeCalls = await _unitOfWork.ActiveCalls.GetBetweenUsersAsync(callerId, targetId, Context.ConnectionAborted);
        var activeCall = activeCalls
            .Where(x => x.CallerId == callerId && x.ReceiverId == targetId)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        if (activeCall is not null)
        {
            var message = await _callHistoryService.CompleteAsync(activeCall, CallStatuses.Missed, Context.ConnectionAborted);
            await PublishCallHistoryAsync(message);
        }

        await Clients.Group(UserGroup(targetId)).SendAsync("CallCancelled", Context.ConnectionAborted);

        var fcmToken = await _unitOfWork.Users.GetFcmTokenByUserIdAsync(targetId, Context.ConnectionAborted);
        if (!string.IsNullOrWhiteSpace(fcmToken))
        {
            await _pushNotificationService.SendCallCancelPushAsync(fcmToken);
        }
    }

    public async Task TimeoutCall(string targetUserId)
    {
        var callerId = GetCurrentUserId();
        var targetId = ParseUserId(targetUserId);
        var activeCalls = await _unitOfWork.ActiveCalls.GetBetweenUsersAsync(callerId, targetId, Context.ConnectionAborted);
        var activeCall = activeCalls
            .Where(x => x.CallerId == callerId && x.ReceiverId == targetId && !x.AnsweredAt.HasValue)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        if (activeCall is null)
        {
            return;
        }

        var message = await _callHistoryService.CompleteAsync(activeCall, CallStatuses.Missed, Context.ConnectionAborted);
        await PublishCallHistoryAsync(message);
        await Clients.Group(UserGroup(callerId)).SendAsync("CallTimedOut", Context.ConnectionAborted);
        await Clients.Group(UserGroup(targetId)).SendAsync("CallCancelled", Context.ConnectionAborted);

        var fcmToken = await _unitOfWork.Users.GetFcmTokenByUserIdAsync(targetId, Context.ConnectionAborted);
        if (!string.IsNullOrWhiteSpace(fcmToken))
        {
            await _pushNotificationService.SendCallCancelPushAsync(fcmToken);
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new HubException("User identifier is missing.");
    }

    private static Guid ParseUserId(string userId)
    {
        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : throw new HubException("Target user identifier is invalid.");
    }

    private static string UserGroup(Guid userId) => $"user:{userId}";

    private async Task NotifyPresenceChangedAsync(Guid userId)
    {
        var conversations = await _unitOfWork.Conversations.GetForUserAsync(userId, Context.ConnectionAborted);
        var viewerIds = conversations
            .Select(conversation => conversation.FirstUserId == userId ? conversation.SecondUserId : conversation.FirstUserId)
            .Distinct()
            .ToList();

        foreach (var viewerId in viewerIds)
        {
            var presence = await _userService.GetVisiblePresenceAsync(userId, viewerId, Context.ConnectionAborted);
            var eventName = presence.IsOnline ? "UserOnline" : "UserOffline";
            await Clients.Group(UserGroup(viewerId))
                .SendAsync(eventName, userId, presence.LastSeenAt, presence.IsActivityHidden, presence.IsLastSeenHidden, Context.ConnectionAborted);
        }
    }

    private async Task SendPendingMessagesAsync(Guid userId)
    {
        var messages = await _unitOfWork.PendingMessages.GetByReceiverIdAsync(userId, Context.ConnectionAborted);

        foreach (var message in messages)
        {
            var isImage = _imageStorageService.IsImagePath(message.Content);
            var isVoice = message.IsVoice && !string.IsNullOrWhiteSpace(message.VoiceUrl) && _imageStorageService.IsVoicePath(message.VoiceUrl);
            await Clients.Caller.SendAsync("ReceiveMessage", new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = isVoice ? "Voice message" : isImage ? "Image" : message.Content,
                SentAt = message.SentAt,
                IsVoice = isVoice,
                VoiceUrl = isVoice ? _imageStorageService.GetPublicUrl(message.VoiceUrl!) : message.VoiceUrl,
                IsImage = isImage,
                ImagePath = isImage ? _imageStorageService.GetPublicUrl(message.Content) : null,
                ImageStoragePath = isImage ? message.Content : null,
                ImageFileName = isImage ? message.VoiceUrl : null,
                IsPending = true,
                IsCall = message.IsCall,
                CallStatus = message.CallStatus,
                CallDurationSeconds = message.CallDurationSeconds
            }, Context.ConnectionAborted);
        }
    }

    private async Task SendActiveCallAsync(Guid userId)
    {
        var activeCall = await _unitOfWork.ActiveCalls.GetByReceiverIdAsync(userId, Context.ConnectionAborted);
        if (activeCall is null)
        {
            return;
        }

        await Clients.Caller.SendAsync(
            "IncomingCall",
            activeCall.CallerId,
            activeCall.CallerName,
            activeCall.CallerAvatar,
            Context.ConnectionAborted);

        if (!string.IsNullOrWhiteSpace(activeCall.OfferSdp))
        {
            await Clients.Caller.SendAsync(
                "CallOffer",
                activeCall.OfferSdp,
                activeCall.CallerId,
                Context.ConnectionAborted);
        }
    }

    private async Task PublishCallHistoryAsync(MessageDto? message)
    {
        if (message is null)
        {
            return;
        }

        await Clients.Groups(UserGroup(message.SenderId), UserGroup(message.ReceiverId))
            .SendAsync("ReceiveMessage", message, Context.ConnectionAborted);
    }

    private MessageDto ToClientMessage(MessageDto message)
    {
        if (message.IsVoice && !string.IsNullOrWhiteSpace(message.VoiceUrl))
        {
            return new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = "Voice message",
                SentAt = message.SentAt,
                ReadAt = message.ReadAt,
                IsVoice = true,
                VoiceUrl = _imageStorageService.GetPublicUrl(message.VoiceUrl),
                VoiceDurationSeconds = message.VoiceDurationSeconds,
                IsImage = false,
                IsPending = message.IsPending
            };
        }

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
            VoiceDurationSeconds = message.VoiceDurationSeconds,
            IsImage = true,
            ImagePath = _imageStorageService.GetPublicUrl(message.ImagePath),
            ImageStoragePath = message.ImageStoragePath ?? message.ImagePath,
            IsPending = message.IsPending,
            ImageFileName = message.ImageFileName
        };
    }

    private async Task DeletePendingMessageMediaAsync(PendingMessage message)
    {
        if (_imageStorageService.IsImagePath(message.Content))
        {
            await _imageStorageService.DeleteAsync(message.Content, Context.ConnectionAborted);
        }

        if (!string.IsNullOrWhiteSpace(message.VoiceUrl) && _imageStorageService.IsVoicePath(message.VoiceUrl))
        {
            await _imageStorageService.DeleteAsync(message.VoiceUrl, Context.ConnectionAborted);
        }
    }
}
