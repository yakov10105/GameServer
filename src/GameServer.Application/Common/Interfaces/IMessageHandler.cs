namespace GameServer.Application.Common.Interfaces;

public interface IMessageHandler
{
    Task<Result> HandleAsync(WebSocket webSocket, JsonElement payload, CancellationToken cancellationToken = default);
}