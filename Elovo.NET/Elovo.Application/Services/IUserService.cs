using Elovo.Application.DTOs;

namespace Elovo.Application.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendCandidateDto>> SearchFriendCandidatesAsync(Guid currentUserId, string? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendRequestDto>> GetIncomingFriendRequestsAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task SendFriendRequestAsync(Guid currentUserId, Guid receiverId, CancellationToken cancellationToken = default);
    Task AcceptFriendRequestAsync(Guid currentUserId, Guid requestId, CancellationToken cancellationToken = default);
    Task SetOnlineStatusAsync(Guid userId, bool isOnline, CancellationToken cancellationToken = default);
}
