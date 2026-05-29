
namespace Elovo.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly ElovoDbContext _context;

    public ConversationRepository(ElovoDbContext context)
    {
        _context = context;
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Conversations
            .Include(x => x.FirstUser)
                .ThenInclude(x => x.Session)
            .Include(x => x.SecondUser)
                .ThenInclude(x => x.Session)
            .Include(x => x.FirstUser)
                .ThenInclude(x => x.TwoFactor)
            .Include(x => x.SecondUser)
                .ThenInclude(x => x.TwoFactor)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Conversation?> GetBetweenUsersAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
    {
        return _context.Conversations
            .FirstOrDefaultAsync(x =>
                (x.FirstUserId == firstUserId && x.SecondUserId == secondUserId) ||
                (x.FirstUserId == secondUserId && x.SecondUserId == firstUserId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Include(x => x.FirstUser)
                .ThenInclude(x => x.Session)
            .Include(x => x.SecondUser)
                .ThenInclude(x => x.Session)
            .Include(x => x.FirstUser)
                .ThenInclude(x => x.TwoFactor)
            .Include(x => x.SecondUser)
                .ThenInclude(x => x.TwoFactor)
            .Where(x => x.FirstUserId == userId || x.SecondUserId == userId)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        return _context.Conversations.AddAsync(conversation, cancellationToken).AsTask();
    }

    public void Update(Conversation conversation)
    {
        _context.Conversations.Update(conversation);
    }

    public void Remove(Conversation conversation)
    {
        _context.Conversations.Remove(conversation);
    }
}
