
namespace Elovo.Application.Services;

public class UserService : IUserService
{
    private const int MinimumPasswordLength = 8;
    private const string HiddenProfileImageUrl = "/Assets/Images/Icons/profile-hidden.svg";
    private const string ActivityVisibilityFull = "full";
    private const string ActivityVisibilityOnlineOnly = "online";
    private const string ActivityVisibilityHidden = "hidden";
    private static readonly TimeSpan EmailCodeLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan EmailSendCooldown = TimeSpan.FromHours(1);
    private static readonly HashSet<string> SupportedActivityVisibilities = new(StringComparer.OrdinalIgnoreCase)
    {
        ActivityVisibilityFull,
        ActivityVisibilityOnlineOnly,
        ActivityVisibilityHidden
    };

    private readonly IEmailSender _emailSender;
    private readonly IImageStorageService _imageStorageService;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserPresenceTracker _presenceTracker;

    public UserService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IImageStorageService imageStorageService,
        IUserPresenceTracker presenceTracker,
        IEmailSender emailSender)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _imageStorageService = imageStorageService;
        _presenceTracker = presenceTracker;
        _emailSender = emailSender;
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

            var presence = GetVisiblePresence(user);

            return new ConversationDto
            {
                Id = conversation.Id,
                UserId = user.Id,
                Username = user.Username,
                IsOnline = presence.IsOnline,
                LastSeenAt = presence.LastSeenAt,
                IsActivityHidden = presence.IsActivityHidden,
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

            var presence = GetVisiblePresence(user);

            items.Add(new FriendCandidateDto
            {
                Id = user.Id,
                Username = user.Username,
                IsOnline = presence.IsOnline,
                LastSeenAt = presence.LastSeenAt,
                IsActivityHidden = presence.IsActivityHidden,
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
        var emailSettings = EnsureEmailSettings(user);
        var emailChanged = !string.Equals(emailSettings.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        if (emailSettings.IsEmailConfirmed && emailChanged)
        {
            throw new InvalidOperationException("Email is already linked and cannot be changed.");
        }

        emailSettings.Email = normalizedEmail;
        if (emailChanged)
        {
            ClearEmailConfirmationCode(emailSettings);
            emailSettings.IsEmailConfirmed = false;
        }

        if (!emailSettings.IsEmailConfirmed)
        {
            await SendEmailConfirmationCodeForUserAsync(user, emailSettings, cancellationToken);
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfileDto(user);
    }

    public async Task<ProfileDto> SendEmailConfirmationCodeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        var emailSettings = EnsureEmailSettings(user);
        if (string.IsNullOrWhiteSpace(emailSettings.Email))
        {
            throw new InvalidOperationException("Add an email before requesting a verification code.");
        }

        if (emailSettings.IsEmailConfirmed)
        {
            return ToProfileDto(user);
        }

        await SendEmailConfirmationCodeForUserAsync(user, emailSettings, cancellationToken);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfileDto(user);
    }

    public async Task<ProfileDto> VerifyEmailAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        var emailSettings = user.EmailSettings;
        if (string.IsNullOrWhiteSpace(emailSettings?.Email) ||
            string.IsNullOrWhiteSpace(emailSettings.EmailConfirmationCodeHash) ||
            emailSettings.EmailConfirmationCodeExpiredAt is null)
        {
            throw new InvalidOperationException("Verification code is invalid.");
        }

        if (emailSettings.EmailConfirmationCodeExpiredAt <= DateTime.UtcNow)
        {
            ClearEmailConfirmationCode(emailSettings);
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Verification code expired.");
        }

        var normalizedCode = NormalizeVerificationCode(code);
        if (normalizedCode.Length != 7 || !BCrypt.Net.BCrypt.Verify(normalizedCode, emailSettings.EmailConfirmationCodeHash))
        {
            throw new InvalidOperationException("Verification code is invalid.");
        }

        emailSettings.IsEmailConfirmed = true;
        ClearEmailConfirmationCode(emailSettings);
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
        var emailSettings = user.EmailSettings;

        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(emailSettings?.Email))
            {
                throw new InvalidOperationException("Add an email before enabling two-factor authentication.");
            }

            if (!emailSettings.IsEmailConfirmed)
            {
                throw new InvalidOperationException("Need to verify email.");
            }
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

    public async Task<ProfileDto> SetActivityVisibilityAsync(Guid userId, string? visibility, CancellationToken cancellationToken = default)
    {
        var normalizedVisibility = NormalizeActivityVisibility(visibility);
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        var session = EnsureSession(user);
        session.ActivityVisibility = normalizedVisibility;

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

    public async Task<UserPresenceDto> SetOnlineStatusAsync(Guid userId, bool isOnline, string? clientIp = null, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return new UserPresenceDto();
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
        return GetVisiblePresence(user);
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
        var presence = GetVisiblePresence(user);
        dto.IsOnline = presence.IsOnline;
        dto.LastSeenAt = presence.LastSeenAt;
        dto.IsActivityHidden = presence.IsActivityHidden;
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
            Email = user.EmailSettings?.Email,
            IsEmailConfirmed = user.EmailSettings?.IsEmailConfirmed ?? false,
            EmailCooldownEndsAt = user.EmailSettings is null ? null : GetEmailCooldownEndsAt(user.EmailSettings),
            ProfileImagePath = user.ProfileImagePath,
            ProfileImageUrl = GetImageUrl(user.ProfileImagePath),
            IsTwoFactorEnabled = user.TwoFactor?.IsTwoFactorEnabled ?? false,
            ActivityVisibility = NormalizeActivityVisibility(user.Session?.ActivityVisibility)
        };
    }

    private UserPresenceDto GetVisiblePresence(User user)
    {
        var visibility = NormalizeActivityVisibility(user.Session?.ActivityVisibility);
        var isOnline = _presenceTracker.IsOnline(user.Id);

        return visibility switch
        {
            ActivityVisibilityHidden => new UserPresenceDto { IsActivityHidden = true },
            ActivityVisibilityOnlineOnly => new UserPresenceDto { IsOnline = isOnline },
            _ => new UserPresenceDto
            {
                IsOnline = isOnline,
                LastSeenAt = isOnline ? null : user.Session?.LastSeenAt
            }
        };
    }

    private static string NormalizeActivityVisibility(string? visibility)
    {
        var normalized = (visibility ?? string.Empty).Trim().ToLowerInvariant();
        return SupportedActivityVisibilities.Contains(normalized) ? normalized : ActivityVisibilityFull;
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

    private async Task SendEmailConfirmationCodeForUserAsync(User user, UserEmail emailSettings, CancellationToken cancellationToken)
    {
        var cooldownEndsAt = GetEmailCooldownEndsAt(emailSettings);
        if (cooldownEndsAt is not null)
        {
            throw new InvalidOperationException(BuildEmailCooldownMessage(cooldownEndsAt.Value));
        }

        var code = GenerateVerificationCode();
        await _emailSender.SendEmailConfirmationCodeAsync(
            emailSettings.Email!,
            user.Username,
            code,
            user.Session?.PreferredLanguage,
            cancellationToken);

        emailSettings.EmailConfirmationCodeHash = BCrypt.Net.BCrypt.HashPassword(code);
        emailSettings.EmailConfirmationCodeExpiredAt = DateTime.UtcNow.Add(EmailCodeLifetime);
        emailSettings.LastEmailSentAt = DateTime.UtcNow;
    }

    private static DateTime? GetEmailCooldownEndsAt(UserEmail emailSettings)
    {
        if (emailSettings.LastEmailSentAt is null)
        {
            return null;
        }

        var endsAt = emailSettings.LastEmailSentAt.Value.Add(EmailSendCooldown);
        return endsAt > DateTime.UtcNow ? endsAt : null;
    }

    private static string GenerateVerificationCode()
    {
        return System.Security.Cryptography.RandomNumberGenerator
            .GetInt32(1_000_000, 10_000_000)
            .ToString();
    }

    private static string NormalizeVerificationCode(string code)
    {
        return new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static void ClearEmailConfirmationCode(UserEmail emailSettings)
    {
        emailSettings.EmailConfirmationCodeHash = null;
        emailSettings.EmailConfirmationCodeExpiredAt = null;
    }

    private static string BuildEmailCooldownMessage(DateTime cooldownEndsAt)
    {
        var remaining = cooldownEndsAt - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"You can request another email in {minutes:D2}:{seconds:D2}.";
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

    private static UserEmail EnsureEmailSettings(User user)
    {
        return user.EmailSettings ??= new UserEmail { UserId = user.Id };
    }
}
