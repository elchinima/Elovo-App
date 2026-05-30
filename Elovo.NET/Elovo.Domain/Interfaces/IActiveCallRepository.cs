namespace Elovo.Domain.Interfaces;

public interface IActiveCallRepository
{
    Task AddAsync(ActiveCall activeCall, CancellationToken cancellationToken = default);
    Task<ActiveCall?> GetByReceiverIdAsync(Guid receiverId, CancellationToken cancellationToken = default);
    Task<ActiveCall?> GetByParticipantsAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<ActiveCall?> GetByParticipantsAsync(Guid callerId, Guid receiverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActiveCall>> GetBetweenUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(ActiveCall activeCall, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<ActiveCall> activeCalls, CancellationToken cancellationToken = default);
}
