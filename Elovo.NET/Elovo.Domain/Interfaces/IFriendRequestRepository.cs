namespace Elovo.Domain.Interfaces;

public interface IFriendRequestRepository
{
    Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FriendRequest?> GetBetweenUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendRequest>> GetIncomingAsync(Guid receiverId, CancellationToken cancellationToken = default);
    Task AddAsync(FriendRequest request, CancellationToken cancellationToken = default);
    void Remove(FriendRequest request);
}
