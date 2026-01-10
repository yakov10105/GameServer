namespace GameServer.Infrastructure.Services;

public sealed class InMemorySessionManager(ILogger<InMemorySessionManager> logger) : ISessionManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _playerToSocket = new(
        Environment.ProcessorCount * 2, 1000);
    private readonly ConcurrentDictionary<WebSocket, Guid> _socketToPlayer = new(
        Environment.ProcessorCount * 2, 1000);

    public void RegisterSession(Guid playerId, WebSocket webSocket)
    {
        _playerToSocket[playerId] = webSocket;
        _socketToPlayer[webSocket] = playerId;
        logger.SessionRegistered(playerId);
    }

    public void RemoveSession(Guid playerId)
    {
        if (_playerToSocket.TryRemove(playerId, out var webSocket))
        {
            _socketToPlayer.TryRemove(webSocket, out _);
            logger.SessionRemoved(playerId);
        }
    }

    public Guid? RemoveBySocket(WebSocket webSocket)
    {
        if (!_socketToPlayer.TryRemove(webSocket, out var playerId))
        {
            return null;
        }

        _playerToSocket.TryRemove(playerId, out _);
        logger.SessionRemoved(playerId);
        return playerId;
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

    public IReadOnlyCollection<Guid> GetAllPlayerIds() => [.. _playerToSocket.Keys];
}

