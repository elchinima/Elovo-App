namespace Elovo.Application.Services;

public class UserPresenceTracker : IUserPresenceTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public bool Connect(Guid userId, string connectionId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(userId, out var connections))
            {
                connections = [];
                _connections[userId] = connections;
            }

            connections.Add(connectionId);
            return connections.Count == 1;
        }
    }

    public bool Disconnect(Guid userId, string connectionId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(userId, out var connections))
            {
                return false;
            }

            connections.Remove(connectionId);
            if (connections.Count > 0)
            {
                return false;
            }

            _connections.Remove(userId);
            return true;
        }
    }

    public bool IsOnline(Guid userId)
    {
        lock (_sync)
        {
            return _connections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }
    }
}
