namespace Elovo.Domain.Interfaces;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IConversationRepository Conversations { get; }
    IFriendRequestRepository FriendRequests { get; }
    IPendingMessageRepository PendingMessages { get; }
    IActiveCallRepository ActiveCalls { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
