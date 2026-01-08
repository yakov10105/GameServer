namespace GameServer.Application.Common.Interfaces;

public interface IMessageDispatcher
{
    Task DispatchAsync(WebSocket webSocket, ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default);
}