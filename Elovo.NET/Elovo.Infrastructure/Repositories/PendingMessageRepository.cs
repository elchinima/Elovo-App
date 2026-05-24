namespace Elovo.Infrastructure.Repositories;

public class PendingMessageRepository : IPendingMessageRepository
{
    private readonly ElovoDbContext _context;

    public PendingMessageRepository(ElovoDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(PendingMessage message, CancellationToken cancellationToken = default)
    {
        return _context.PendingMessages.AddAsync(message, cancellationToken).AsTask();
    }

    public Task<PendingMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.PendingMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingMessage>> GetByReceiverIdAsync(Guid receiverId, CancellationToken cancellationToken = default)
    {
        return await _context.PendingMessages
            .Where(x => x.ReceiverId == receiverId)
            .OrderBy(x => x.SentAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteRangeAsync(IEnumerable<PendingMessage> messages, CancellationToken cancellationToken = default)
    {
        _context.PendingMessages.RemoveRange(messages);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PendingMessage message, CancellationToken cancellationToken = default)
    {
        _context.PendingMessages.Remove(message);
        return Task.CompletedTask;
    }
}
