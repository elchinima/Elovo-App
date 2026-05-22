using Elovo.Domain.Interfaces;
using Elovo.Infrastructure.Data;

namespace Elovo.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly ElovoDbContext _context;

    public UnitOfWork(
        ElovoDbContext context,
        IUserRepository users,
        IConversationRepository conversations,
        IFriendRequestRepository friendRequests)
    {
        _context = context;
        Users = users;
        Conversations = conversations;
        FriendRequests = friendRequests;
    }

    public IUserRepository Users { get; }
    public IConversationRepository Conversations { get; }
    public IFriendRequestRepository FriendRequests { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
