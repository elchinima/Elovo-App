namespace Elovo.Domain.Interfaces;

public interface IPendingMessageRepository
{
    Task AddAsync(PendingMessage message, CancellationToken cancellationToken = default);
    Task<PendingMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingMessage>> GetByReceiverIdAsync(Guid receiverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingMessage>> GetBetweenUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(PendingMessage message, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<PendingMessage> messages, CancellationToken cancellationToken = default);
}
