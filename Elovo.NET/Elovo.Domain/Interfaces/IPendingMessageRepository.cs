namespace Elovo.Domain.Interfaces;

public interface IPendingMessageRepository
{
    Task AddAsync(PendingMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingMessage>> GetByReceiverIdAsync(Guid receiverId, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<PendingMessage> messages, CancellationToken cancellationToken = default);
}
