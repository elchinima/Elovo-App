namespace Elovo.Infrastructure.Repositories;

public class ActiveCallRepository : IActiveCallRepository
{
    private readonly ElovoDbContext _context;

    public ActiveCallRepository(ElovoDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(ActiveCall activeCall, CancellationToken cancellationToken = default)
    {
        return _context.ActiveCalls.AddAsync(activeCall, cancellationToken).AsTask();
    }

    public Task<ActiveCall?> GetByReceiverIdAsync(Guid receiverId, CancellationToken cancellationToken = default)
    {
        return _context.ActiveCalls
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(x => x.ReceiverId == receiverId && !x.IsRejected, cancellationToken);
    }

    public async Task<ActiveCall?> GetByCallerIdAsync(Guid callerId, CancellationToken cancellationToken = default)
    {
        return await _context.ActiveCalls
            .Where(x => x.CallerId == callerId && !x.IsRejected)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ActiveCall?> GetByParticipantsAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        return _context.ActiveCalls
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(
                x => (x.CallerId == participantId || x.ReceiverId == participantId) && !x.IsRejected,
                cancellationToken);
    }

    public Task<ActiveCall?> GetByParticipantsAsync(Guid callerId, Guid receiverId, CancellationToken cancellationToken = default)
    {
        return _context.ActiveCalls.FirstOrDefaultAsync(
            x => x.CallerId == callerId && x.ReceiverId == receiverId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveCall>> GetBetweenUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
    {
        return await _context.ActiveCalls
            .Where(x =>
                (x.CallerId == firstUserId && x.ReceiverId == secondUserId) ||
                (x.CallerId == secondUserId && x.ReceiverId == firstUserId))
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(ActiveCall activeCall, CancellationToken cancellationToken = default)
    {
        _context.ActiveCalls.Remove(activeCall);
        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(IEnumerable<ActiveCall> activeCalls, CancellationToken cancellationToken = default)
    {
        _context.ActiveCalls.RemoveRange(activeCalls);
        return Task.CompletedTask;
    }
}
