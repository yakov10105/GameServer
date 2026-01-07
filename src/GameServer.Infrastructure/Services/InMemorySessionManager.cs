
namespace GameServer.Infrastructure.Services;

public sealed class InMemorySessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _playerToSocket;
    private readonly ConcurrentDictionary<WebSocket, Guid> _socketToPlayer;

    public InMemorySessionManager()
    {
        var concurrencyLevel = Environment.ProcessorCount * 2;
        var initialCapacity = 1000;
        
        _playerToSocket = new ConcurrentDictionary<Guid, WebSocket>(concurrencyLevel, initialCapacity);
        _socketToPlayer = new ConcurrentDictionary<WebSocket, Guid>(concurrencyLevel, initialCapacity);
    }

    public void RegisterSession(Guid playerId, WebSocket webSocket)
    {
        _playerToSocket[playerId] = webSocket;
        _socketToPlayer[webSocket] = playerId;
    }

    public void RemoveSession(Guid playerId)
    {
        if (_playerToSocket.TryRemove(playerId, out var webSocket))
        {
            _socketToPlayer.TryRemove(webSocket, out _);
        }
    }

    public Guid? GetPlayerId(WebSocket webSocket)
    {
        return _socketToPlayer.TryGetValue(webSocket, out var playerId) ? playerId : null;
    }

    public WebSocket? GetSocket(Guid playerId)
    {
        return _playerToSocket.TryGetValue(playerId, out var webSocket) ? webSocket : null;
    }

    public bool IsPlayerOnline(Guid playerId)
    {
        return _playerToSocket.ContainsKey(playerId);
    }

    public IReadOnlyCollection<Guid> GetAllPlayerIds() => _playerToSocket.Keys.ToArray();
}

