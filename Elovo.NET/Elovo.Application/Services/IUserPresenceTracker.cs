namespace Elovo.Application.Services;

public interface IUserPresenceTracker
{
    bool Connect(Guid userId, string connectionId);
    bool Disconnect(Guid userId, string connectionId);
    bool IsOnline(Guid userId);
}
