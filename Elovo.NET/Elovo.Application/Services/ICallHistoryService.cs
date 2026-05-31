namespace Elovo.Application.Services;

public interface ICallHistoryService
{
    Task<MessageDto?> CompleteAsync(ActiveCall activeCall, string status, CancellationToken cancellationToken = default);
}
