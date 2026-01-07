namespace GameServer.Domain.Interfaces;

public interface IGameNotifier
{
    Task SendToPlayerAsync(Guid playerId, ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default);
    
    Task BroadcastAsync(ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default);
    
    Task BroadcastExceptAsync(Guid excludePlayerId, ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default);
}

