
namespace Elovo.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ElovoDbContext _context;

    public UserRepository(ElovoDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return UsersWithDetails().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return UsersWithDetails().FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLower();
        return UsersWithDetails()
            .FirstOrDefaultAsync(x => x.EmailSettings != null &&
                x.EmailSettings.Email != null &&
                x.EmailSettings.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public Task<string?> GetFcmTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.UserSessions
            .Where(session => session.UserId == userId)
            .Select(session => session.FcmToken)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllExceptAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        return await UsersWithDetails()
            .Where(x => x.Id != currentUserId)
            .OrderBy(x => x.Username)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return _context.Users.AddAsync(user, cancellationToken).AsTask();
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }

    private IQueryable<User> UsersWithDetails()
    {
        return _context.Users
            .Include(x => x.Session)
            .Include(x => x.TwoFactor)
            .Include(x => x.EmailSettings)
            .Include(x => x.Premium);
    }
}
