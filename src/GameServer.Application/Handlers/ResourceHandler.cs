
namespace GameServer.Application.Handlers;

public sealed class ResourceHandler : IMessageHandler
{
    public Task<Result> HandleAsync(WebSocket webSocket, JsonElement payload, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
