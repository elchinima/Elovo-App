namespace Elovo.Application.Services;

public class CallHistoryService : ICallHistoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CallHistoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<MessageDto?> CompleteAsync(ActiveCall activeCall, string status, CancellationToken cancellationToken = default)
    {
        if (!CallStatuses.IsSupported(status))
        {
            throw new InvalidOperationException("Call status is invalid.");
        }

        var conversation = await _unitOfWork.Conversations.GetBetweenUsersAsync(
            activeCall.CallerId,
            activeCall.ReceiverId,
            cancellationToken);
        if (conversation is null)
        {
            await _unitOfWork.ActiveCalls.DeleteAsync(activeCall, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        var completedAt = DateTime.UtcNow;
        var durationSeconds = activeCall.AnsweredAt.HasValue
            ? Math.Max(0, (completedAt - activeCall.AnsweredAt.Value).TotalSeconds)
            : 0;
        var receiver = await _unitOfWork.Users.GetByIdAsync(activeCall.ReceiverId, cancellationToken);
        var message = new MessageDto
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = activeCall.CallerId,
            ReceiverId = activeCall.ReceiverId,
            Content = BuildContent(status, durationSeconds, receiver?.Session?.PreferredLanguage),
            SentAt = completedAt,
            IsPending = true,
            IsCall = true,
            CallStatus = status,
            CallDurationSeconds = durationSeconds
        };

        await _unitOfWork.PendingMessages.AddAsync(new PendingMessage
        {
            Id = message.Id,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content,
            SentAt = message.SentAt,
            IsCall = true,
            CallStatus = message.CallStatus,
            CallDurationSeconds = message.CallDurationSeconds
        }, cancellationToken);
        await _unitOfWork.ActiveCalls.DeleteAsync(activeCall, cancellationToken);
        conversation.UpdatedAt = completedAt;
        _unitOfWork.Conversations.Update(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return message;
    }

    private static string BuildContent(string status, double durationSeconds, string? language)
    {
        var label = status switch
        {
            CallStatuses.Answered => "Answered call",
            CallStatuses.Rejected => "Rejected call",
            _ => "Missed call"
        };

        var duration = TimeSpan.FromSeconds(Math.Max(0, durationSeconds));
        return $"{PushLocalization.GetText(label, language)} - {(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }
}
