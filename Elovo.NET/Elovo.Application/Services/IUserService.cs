
namespace Elovo.Application.Services;

public interface IUserService
{
    Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetUsersAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendCandidateDto>> SearchFriendCandidatesAsync(Guid currentUserId, string? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendRequestDto>> GetIncomingFriendRequestsAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<ProfileDto> UpdateEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<ProfileDto> SetTwoFactorEnabledAsync(Guid userId, bool enabled, CancellationToken cancellationToken = default);
    Task<ProfileDto> SetProfileImagePathAsync(Guid userId, string path, CancellationToken cancellationToken = default);
    Task<ProfileDto> RemoveProfileImagePathAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SendFriendRequestAsync(Guid currentUserId, Guid receiverId, CancellationToken cancellationToken = default);
    Task AcceptFriendRequestAsync(Guid currentUserId, Guid requestId, CancellationToken cancellationToken = default);
    Task<DateTime?> SetOnlineStatusAsync(Guid userId, bool isOnline, string? clientIp = null, CancellationToken cancellationToken = default);
}
