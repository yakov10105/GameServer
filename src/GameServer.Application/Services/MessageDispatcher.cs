namespace GameServer.Application.Services;

public sealed class MessageDispatcher(
    IServiceProvider serviceProvider
    ) : IMessageDispatcher
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public async Task DispatchAsync(WebSocket webSocket, ReadOnlyMemory<byte> messageBytes, CancellationToken cancellationToken = default)
    {
        if (messageBytes.IsEmpty)
            return;

        MessageEnvelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<MessageEnvelope>(messageBytes.Span, _jsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (envelope is null || string.IsNullOrEmpty(envelope.Type))
            return;

        var handler = serviceProvider.GetKeyedService<IMessageHandler>(envelope.Type);

        if (handler is null)
            return;

        await handler.HandleAsync(webSocket, envelope.Payload, cancellationToken)
            .ConfigureAwait(false);

        // TODO Phase 4: Send error response to client
    }
}