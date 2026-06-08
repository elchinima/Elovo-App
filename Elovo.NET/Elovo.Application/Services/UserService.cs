
namespace Elovo.Application.Services;

public class UserService : IUserService
{
    private const int MinimumPasswordLength = 8;
    private const string HiddenProfileImageUrl = "/Assets/Images/Icons/profile-hidden.svg";

    private readonly IImageStorageService _imageStorageService;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserPresenceTracker _presenceTracker;

    public UserService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IImageStorageService imageStorageService,
        IUserPresenceTracker presenceTracker)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _imageStorageService = imageStorageService;
        _presenceTracker = presenceTracker;
    }

    public async Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        return ToProfileDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.GetAllExceptAsync(currentUserId, cancellationToken);
        var conversations = await _unitOfWork.Conversations.GetForUserAsync(currentUserId, cancellationToken);
        var friendIds = GetFriendIds(currentUserId, conversations);
        return users.Select(user => ToUserDto(user, friendIds.Contains(user.Id))).ToList();
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var conversations = await _unitOfWork.Conversations.GetForUserAsync(currentUserId, cancellationToken);

        var items = conversations.Select(conversation =>
        {
            var user = conversation.FirstUserId == currentUserId
                ? conversation.SecondUser
                : conversation.FirstUser;

            return new ConversationDto
            {
                Id = conversation.Id,
                UserId = user.Id,
                Username = user.Username,
                IsOnline = _presenceTracker.IsOnline(user.Id),
                LastSeenAt = user.Session?.LastSeenAt,
                ProfileImagePath = user.ProfileImagePath,
                ProfileImageUrl = GetImageUrl(user.ProfileImagePath),
                LastMessage = "Start a conversation.",
                LastMessageAt = null,
                OtherUserReadAt = conversation.FirstUserId == user.Id
                    ? conversation.FirstUserReadAt
                    : conversation.SecondUserReadAt,
                UnreadCount = 0
            };
        });

        return items
            .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
            .ThenBy(x => x.Username)
            .ToList();
    }

    public async Task<IReadOnlyList<FriendCandidateDto>> SearchFriendCandidatesAsync(Guid currentUserId, string? query, CancellationToken cancellationToken = default)
    {
        var term = (query ?? string.Empty).Trim();
        if (term.Length < 1)
        {
            return Array.Empty<FriendCandidateDto>();
        }

        var users = await _unitOfWork.Users.GetAllExceptAsync(currentUserId, cancellationToken);
        var conversations = await _unitOfWork.Conversations.GetForUserAsync(currentUserId, cancellationToken);
        var friendIds = GetFriendIds(currentUserId, conversations);

        var items = new List<FriendCandidateDto>();

        foreach (var user in users.Where(x => x.Username.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            var request = await _unitOfWork.FriendRequests.GetBetweenUsersAsync(currentUserId, user.Id, cancellationToken);
            var isFriend = friendIds.Contains(user.Id);

            items.Add(new FriendCandidateDto
            {
                Id = user.Id,
                Username = user.Username,
                IsOnline = _presenceTracker.IsOnline(user.Id),
                LastSeenAt = user.Session?.LastSeenAt,
                ProfileImagePath = isFriend ? user.ProfileImagePath : null,
                ProfileImageUrl = GetVisibleProfileImageUrl(user.ProfileImagePath, isFriend),
                Status = isFriend
                    ? "friend"
                    : request is null
                        ? "none"
                        : request.SenderId == currentUserId ? "sent" : "incoming"
            });
        }

        return items
            .OrderBy(x => x.Username)
            .Take(20)
            .ToList();
    }

    public async Task<IReadOnlyList<FriendRequestDto>> GetIncomingFriendRequestsAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var requests = await _unitOfWork.FriendRequests.GetIncomingAsync(currentUserId, cancellationToken);

        return requests.Select(x => new FriendRequestDto
        {
            Id = x.Id,
            SenderId = x.SenderId,
            SenderUsername = x.Sender.Username,
            ProfileImagePath = null,
            ProfileImageUrl = GetVisibleProfileImageUrl(x.Sender.ProfileImagePath, false),
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    public async Task<ProfileDto> UpdateEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length > 256 || !IsValidEmail(normalizedEmail))
        {
            throw new InvalidOperationException("Email is invalid.");
        }

        var existingUser = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null && existingUser.Id != userId)
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var user = await GetRequiredUserAsync(userId, cancellationToken);
        user.Email = normalizedEmail;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfileDto(user);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinimumPasswordLength || newPassword.Length > 128)
        {
            throw new InvalidOperationException("New password is invalid.");
        }

        var user = await GetRequiredUserAsync(userId, cancellationToken);
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("Current password is invalid.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProfileDto> SetTwoFactorEnabledAsync(Guid userId, bool enabled, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        var twoFactor = EnsureTwoFactor(user);

        if (enabled && string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("Add an email before enabling two-factor authentication.");
        }

        twoFactor.IsTwoFactorEnabled = enabled;
        if (!enabled)
        {
            twoFactor.TwoFactorCodeHash = null;
            twoFactor.TwoFactorCodeExpiredAt = null;
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfileDto(user);
    }

    public async Task<ProfileDto> SetProfileImagePathAsync(Guid userId, string path, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        user.ProfileImagePath = path;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToProfileDto(user);
    }

    public async Task<ProfileDto> RemoveProfileImagePathAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        user.ProfileImagePath = null;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToProfileDto(user);
    }

    public async Task SendFriendRequestAsync(Guid currentUserId, Guid receiverId, CancellationToken cancellationToken = default)
    {
        if (currentUserId == receiverId)
        {
            throw new InvalidOperationException("You cannot add yourself.");
        }

        _ = await _unitOfWork.Users.GetByIdAsync(receiverId, cancellationToken)
            ?? throw new InvalidOperationException("User was not found.");

        var conversation = await _unitOfWork.Conversations.GetBetweenUsersAsync(currentUserId, receiverId, cancellationToken);
        if (conversation is not null)
        {
            return;
        }

        var existing = await _unitOfWork.FriendRequests.GetBetweenUsersAsync(currentUserId, receiverId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await _unitOfWork.FriendRequests.AddAsync(new FriendRequest
        {
            Id = Guid.NewGuid(),
            SenderId = currentUserId,
            ReceiverId = receiverId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptFriendRequestAsync(Guid currentUserId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _unitOfWork.FriendRequests.GetByIdAsync(requestId, cancellationToken)
            ?? throw new InvalidOperationException("Friend request was not found.");

        if (request.ReceiverId != currentUserId)
        {
            throw new InvalidOperationException("Friend request does not belong to this user.");
        }

        var conversation = await _unitOfWork.Conversations.GetBetweenUsersAsync(request.SenderId, request.ReceiverId, cancellationToken);
        if (conversation is null)
        {
            var (firstUserId, secondUserId) = SortUserIds(request.SenderId, request.ReceiverId);
            await _unitOfWork.Conversations.AddAsync(new Conversation
            {
                Id = Guid.NewGuid(),
                FirstUserId = firstUserId,
                SecondUserId = secondUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        _unitOfWork.FriendRequests.Remove(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFriendAsync(Guid currentUserId, Guid friendId, CancellationToken cancellationToken = default)
    {
        if (currentUserId == friendId)
        {
            throw new InvalidOperationException("Friend identifier is invalid.");
        }

        var conversation = await _unitOfWork.Conversations.GetBetweenUsersAsync(currentUserId, friendId, cancellationToken)
            ?? throw new InvalidOperationException("Friend was not found.");
        var requests = await _unitOfWork.FriendRequests.GetAllBetweenUsersAsync(currentUserId, friendId, cancellationToken);
        var pendingMessages = await _unitOfWork.PendingMessages.GetBetweenUsersAsync(currentUserId, friendId, cancellationToken);
        var activeCalls = await _unitOfWork.ActiveCalls.GetBetweenUsersAsync(currentUserId, friendId, cancellationToken);

        foreach (var message in pendingMessages)
        {
            await DeletePendingMessageMediaAsync(message, cancellationToken);
        }

        if (pendingMessages.Count > 0)
        {
            await _unitOfWork.PendingMessages.DeleteRangeAsync(pendingMessages, cancellationToken);
        }

        if (activeCalls.Count > 0)
        {
            await _unitOfWork.ActiveCalls.DeleteRangeAsync(activeCalls, cancellationToken);
        }

        if (requests.Count > 0)
        {
            _unitOfWork.FriendRequests.RemoveRange(requests);
        }

        _unitOfWork.Conversations.Remove(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTime?> SetOnlineStatusAsync(Guid userId, bool isOnline, string? clientIp = null, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var session = EnsureSession(user);
        session.IsOnline = isOnline;
        session.LastSeenAt = isOnline ? null : DateTime.UtcNow;
        if (isOnline)
        {
            ApplyLoginIp(session, clientIp);
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return session.LastSeenAt;
    }

    private static (Guid FirstUserId, Guid SecondUserId) SortUserIds(Guid firstUserId, Guid secondUserId)
    {
        return firstUserId.CompareTo(secondUserId) <= 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);
    }

    private async Task<User> GetRequiredUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User was not found.");
    }

    private UserDto ToUserDto(User user, bool canSeeProfileImage)
    {
        var dto = _mapper.Map<UserDto>(user);
        dto.IsOnline = _presenceTracker.IsOnline(user.Id);
        dto.ProfileImagePath = canSeeProfileImage ? user.ProfileImagePath : null;
        dto.ProfileImageUrl = GetVisibleProfileImageUrl(user.ProfileImagePath, canSeeProfileImage);
        return dto;
    }

    private static HashSet<Guid> GetFriendIds(Guid currentUserId, IEnumerable<Conversation> conversations)
    {
        return conversations
            .Select(x => x.FirstUserId == currentUserId ? x.SecondUserId : x.FirstUserId)
            .ToHashSet();
    }

    private string? GetVisibleProfileImageUrl(string? path, bool canSeeProfileImage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return canSeeProfileImage ? GetImageUrl(path) : HiddenProfileImageUrl;
    }

    private ProfileDto ToProfileDto(User user)
    {
        return new ProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            ProfileImagePath = user.ProfileImagePath,
            ProfileImageUrl = GetImageUrl(user.ProfileImagePath),
            IsTwoFactorEnabled = user.TwoFactor?.IsTwoFactorEnabled ?? false
        };
    }

    private string? GetImageUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return _imageStorageService.GetPublicUrl(path);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyLoginIp(UserSession session, string? clientIp)
    {
        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            session.LastLoginIp = clientIp;
        }

        if (string.IsNullOrWhiteSpace(session.RegistrationIp))
        {
            session.RegistrationIp = session.LastLoginIp;
        }
    }

    private async Task DeletePendingMessageMediaAsync(PendingMessage message, CancellationToken cancellationToken)
    {
        if (_imageStorageService.IsImagePath(message.Content))
        {
            await _imageStorageService.DeleteAsync(message.Content, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(message.VoiceUrl) && _imageStorageService.IsVoicePath(message.VoiceUrl))
        {
            await _imageStorageService.DeleteAsync(message.VoiceUrl, cancellationToken);
        }
    }

    private static UserSession EnsureSession(User user)
    {
        return user.Session ??= new UserSession { UserId = user.Id };
    }

    private static UserTwoFactor EnsureTwoFactor(User user)
    {
        return user.TwoFactor ??= new UserTwoFactor { UserId = user.Id };
    }
}
