using GameServer.Api.Configuration;
using Microsoft.Extensions.Options;

namespace GameServer.Api.Middleware;

public sealed class WebSocketMiddleware
{
    private static int _activeConnections;
    
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISessionManager _sessionManager;
    private readonly GameServerOptions _options;
    private readonly SemaphoreSlim _connectionLimiter;

    public WebSocketMiddleware(
        RequestDelegate next,
        ILogger<WebSocketMiddleware> logger,
        IServiceScopeFactory serviceScopeFactory,
        ISessionManager sessionManager,
        IOptions<GameServerOptions> options)
    {
        _next = next;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _sessionManager = sessionManager;
        _options = options.Value;
        _connectionLimiter = new SemaphoreSlim(_options.MaxConnections, _options.MaxConnections);
    }

    public static int ActiveConnections => _activeConnections;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
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
            _logger.ConnectionAccepted(_activeConnections);

            webSocket = await context.WebSockets.AcceptWebSocketAsync();
            
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var messageDispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
            
            await HandleMessageAsync(webSocket, messageDispatcher, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected");
        }
        catch (WebSocketException) when (webSocket?.State != WebSocketState.Open)
        {
            _logger.LogInformation("Client connection closed abruptly");
        }
        catch (Exception ex)
        {
            _logger.WebSocketError(Guid.Empty, ex.Message, ex);
        }
        finally
        {
            if (webSocket is not null)
            {
                var removedPlayerId = _sessionManager.RemoveBySocket(webSocket);
                if (removedPlayerId.HasValue)
                {
                    _logger.SessionCleanedUp(removedPlayerId.Value);
                }
            }

            if (webSocket?.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closing",
                    CancellationToken.None);
            }

            webSocket?.Dispose();

            Interlocked.Decrement(ref _activeConnections);
            _connectionLimiter.Release();
        }
    }

    private async Task HandleMessageAsync(
        WebSocket webSocket, 
        IMessageDispatcher messageDispatcher,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            while (webSocket.State is WebSocketState.Open)
            {
                var stopwatch = Stopwatch.StartNew();
                var message = await ReceiveFullMessageAsync(webSocket, buffer, _options.MaxMessageSizeBytes, cancellationToken);
                var latencyMs = stopwatch.Elapsed.TotalMilliseconds;

                _logger.MessageReceived(message.Length, latencyMs);

                if (latencyMs > _options.LatencyThresholdMs)
                {
                    _logger.SlowMessageProcessing(latencyMs, _options.LatencyThresholdMs);
                }

                await messageDispatcher.DispatchAsync(webSocket, message, cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReceiveFullMessageAsync(
        WebSocket webSocket, 
        byte[] buffer, 
        int maxMessageSizeBytes,
        CancellationToken cancellationToken)
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

            if (messageBuffer.Length > maxMessageSizeBytes)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    "Message size exceeds limit",
                    cancellationToken);

                throw new InvalidOperationException("Message too large");
            }

        } while (!result.EndOfMessage);

        return messageBuffer.ToArray();
    }
}