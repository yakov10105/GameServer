namespace GameServer.Domain.Interfaces;

public interface ISessionManager
{
    void RegisterSession(Guid playerId, WebSocket webSocket);
    
    void RemoveSession(Guid playerId);
    
    Guid? RemoveBySocket(WebSocket webSocket);
    
    Guid? GetPlayerId(WebSocket webSocket);
    
    WebSocket? GetSocket(Guid playerId);
    
    bool IsPlayerOnline(Guid playerId);

    IReadOnlyCollection<Guid> GetAllPlayerIds();
}

