
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
        return _context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllExceptAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
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
}
