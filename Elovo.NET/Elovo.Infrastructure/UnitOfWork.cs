
namespace Elovo.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly ElovoDbContext _context;

    public UnitOfWork(
        ElovoDbContext context,
        IUserRepository users,
        IConversationRepository conversations,
        IFriendRequestRepository friendRequests,
        IPendingMessageRepository pendingMessages)
    {
        _context = context;
        Users = users;
        Conversations = conversations;
        FriendRequests = friendRequests;
        PendingMessages = pendingMessages;
    }

    public IUserRepository Users { get; }
    public IConversationRepository Conversations { get; }
    public IFriendRequestRepository FriendRequests { get; }
    public IPendingMessageRepository PendingMessages { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
