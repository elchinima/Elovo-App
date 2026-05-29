
namespace Elovo.Infrastructure.Repositories;

public class FriendRequestRepository : IFriendRequestRepository
{
    private readonly ElovoDbContext _context;

    public FriendRequestRepository(ElovoDbContext context)
    {
        _context = context;
    }

    public Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.FriendRequests
            .Include(x => x.Sender)
            .Include(x => x.Receiver)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<FriendRequest?> GetBetweenUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
    {
        return _context.FriendRequests
            .Include(x => x.Sender)
            .Include(x => x.Receiver)
            .FirstOrDefaultAsync(x =>
                (x.SenderId == firstUserId && x.ReceiverId == secondUserId) ||
                (x.SenderId == secondUserId && x.ReceiverId == firstUserId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<FriendRequest>> GetAllBetweenUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
    {
        return await _context.FriendRequests
            .Where(x =>
                (x.SenderId == firstUserId && x.ReceiverId == secondUserId) ||
                (x.SenderId == secondUserId && x.ReceiverId == firstUserId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FriendRequest>> GetIncomingAsync(Guid receiverId, CancellationToken cancellationToken = default)
    {
        return await _context.FriendRequests
            .Include(x => x.Sender)
            .Where(x => x.ReceiverId == receiverId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(FriendRequest request, CancellationToken cancellationToken = default)
    {
        return _context.FriendRequests.AddAsync(request, cancellationToken).AsTask();
    }

    public void Remove(FriendRequest request)
    {
        _context.FriendRequests.Remove(request);
    }

    public void RemoveRange(IEnumerable<FriendRequest> requests)
    {
        _context.FriendRequests.RemoveRange(requests);
    }
}
