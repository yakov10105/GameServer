

using GameServer.Application.Common;

namespace GameServer.Api.Middleware;

/// <summary>
/// Handles raw WebSocket connections with connection limiting and latency monitoring.
/// Capacity: 1000 concurrent connections. Messages exceeding 1MB are rejected.
/// </summary>
public sealed class WebSocketMiddleware(
    RequestDelegate next,
    ILogger<WebSocketMiddleware> logger,
    IMessageDispatcher messageDispatcher
    )
{
    private static readonly SemaphoreSlim _connectionLimiter = new(1000, 1000);
    private static int _activeConnections = 0;
    private const int LatencyThresholdMs = 50;

    public static int ActiveConnections => _activeConnections;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await next(context);
            return;
        }

        if (!await _connectionLimiter.WaitAsync(0))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        WebSocket? webSocket = null;

        try
        {
            Interlocked.Increment(ref _activeConnections);
            logger.ConnectionAccepted(_activeConnections);

            webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleMessageAsync(webSocket, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.WebSocketError(Guid.Empty, ex.Message, ex);
        }
        finally
        {
            if (webSocket?.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closing",
                    CancellationToken.None);
            }

            Interlocked.Decrement(ref _activeConnections);
            _connectionLimiter.Release();
        }

    }

    private async Task HandleMessageAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            while (webSocket.State is WebSocketState.Open)
            {
                var stopwatch = Stopwatch.StartNew();
                var message = await ReceiveFullMessageAsync(webSocket, buffer, cancellationToken);
                var latencyMs = stopwatch.Elapsed.TotalMilliseconds;

                logger.MessageReceived("Unknown", message.Length, latencyMs);

                if (latencyMs > LatencyThresholdMs)
                {
                    logger.SlowMessageProcessing("Unknown", latencyMs, LatencyThresholdMs);
                }

                await messageDispatcher.DispatchAsync(webSocket, message, cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Receives a complete WebSocket message, handling chunking if message spans multiple frames.
    /// Enforces 1MB maximum message size to prevent memory exhaustion attacks.
    /// </summary>
    private static async Task<ReadOnlyMemory<byte>> ReceiveFullMessageAsync(WebSocket webSocket, byte[] buffer, CancellationToken cancellationToken)
    {
        var messageBuffer = new MemoryStream();
        ValueWebSocketReceiveResult result;

        do
        {
            result = await webSocket.ReceiveAsync(buffer.AsMemory(), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client requested close",
                    cancellationToken);
                return ReadOnlyMemory<byte>.Empty;
            }

            messageBuffer.Write(buffer, 0, result.Count);

            if (messageBuffer.Length > 1024 * 1024)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    "Message size exceeds 1MB limit",
                    cancellationToken);

                throw new InvalidOperationException("Message too large");
            }

        } while (!result.EndOfMessage);

        return messageBuffer.ToArray();
    }
}