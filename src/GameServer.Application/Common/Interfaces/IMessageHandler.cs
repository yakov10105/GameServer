namespace GameServer.Application.Common.Interfaces;

public interface IMessageHandler
{
    ValueTask<Result> HandleAsync(WebSocket webSocket, JsonElement payload, CancellationToken cancellationToken = default);
}