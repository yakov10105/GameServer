namespace GameServer.Application.Services;

public sealed class MessageDispatcher(
    IServiceProvider serviceProvider,
    ILogger<MessageDispatcher> logger) : IMessageDispatcher
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
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize message");
            await SendErrorAsync(webSocket, "InvalidMessage", "Failed to parse message", cancellationToken);
            return;
        }

        if (envelope is null || string.IsNullOrEmpty(envelope.Type))
        {
            logger.LogWarning("Received message with missing type");
            await SendErrorAsync(webSocket, "InvalidMessage", "Message type is required", cancellationToken);
            return;
        }

        logger.LogDebug("Dispatching message type: {Type}", envelope.Type);

        var handler = serviceProvider.GetKeyedService<IMessageHandler>(envelope.Type);

        if (handler is null)
        {
            logger.LogWarning("No handler found for message type: {Type}", envelope.Type);
            await SendErrorAsync(webSocket, "UnknownType", $"Unknown message type: {envelope.Type}", cancellationToken);
            return;
        }

        var result = await handler.HandleAsync(webSocket, envelope.Payload, cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Handler {Type} failed: {Code} - {Message}", 
                envelope.Type, result.Error?.Code, result.Error?.Message);
            
            await SendErrorAsync(webSocket, 
                result.Error?.Code ?? "Error", 
                result.Error?.Message ?? "An error occurred", 
                cancellationToken);
            return;
        }

        logger.LogDebug("Handler {Type} completed successfully", envelope.Type);
    }

    private static async Task SendErrorAsync(
        WebSocket webSocket, 
        string code, 
        string message, 
        CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        var response = new ServerMessage<ErrorPayload>(
            MessageTypes.Error,
            new ErrorPayload(code, message));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonSerializerOptionsProvider.Default);
        
        await webSocket.SendAsync(
            bytes.AsMemory(),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }
}