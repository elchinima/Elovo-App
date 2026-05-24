
namespace Elovo.Application.Services;

public class UserService : IUserService
{
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.GetAllExceptAsync(currentUserId, cancellationToken);
        return _mapper.Map<IReadOnlyList<UserDto>>(users);
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
                IsOnline = user.IsOnline,
                LastSeenAt = user.LastSeenAt,
                LastMessage = "Start a conversation.",
                LastMessageAt = null,
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
        var friendIds = conversations
            .Select(x => x.FirstUserId == currentUserId ? x.SecondUserId : x.FirstUserId)
            .ToHashSet();

        var items = new List<FriendCandidateDto>();

        foreach (var user in users.Where(x => x.Username.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            var request = await _unitOfWork.FriendRequests.GetBetweenUsersAsync(currentUserId, user.Id, cancellationToken);

            items.Add(new FriendCandidateDto
            {
                Id = user.Id,
                Username = user.Username,
                IsOnline = user.IsOnline,
                LastSeenAt = user.LastSeenAt,
                Status = friendIds.Contains(user.Id)
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
            CreatedAt = x.CreatedAt
        }).ToList();
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

    public async Task<DateTime?> SetOnlineStatusAsync(Guid userId, bool isOnline, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.IsOnline = isOnline;
        user.LastSeenAt = isOnline ? null : DateTime.UtcNow;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return user.LastSeenAt;
    }

    private static (Guid FirstUserId, Guid SecondUserId) SortUserIds(Guid firstUserId, Guid secondUserId)
    {
        return firstUserId.CompareTo(secondUserId) <= 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);
    }
}
