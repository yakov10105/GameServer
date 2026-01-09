using GameServer.ConsoleClient.Model.Request;

namespace GameServer.ConsoleClient.Clients;

public sealed class GameClient(
    TimeSpan? connectionTimeout = null,
    TimeSpan? sendTimeout = null,
    int maxRetryAttempts = 3,
    TimeSpan? retryDelay = null) : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket = new();
    private readonly TimeSpan _connectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(10);
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
    private bool _isDisposed;
    private readonly TimeSpan _sendTimeout = sendTimeout ?? TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WebSocketState State => _webSocket.State;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    public async Task<bool> ConnectAsync(Uri serverUri, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        for (var attempt = 1; attempt <= maxRetryAttempts; attempt++)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(_connectionTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCts.Token);

                await _webSocket.ConnectAsync(serverUri, linkedCts.Token).ConfigureAwait(false);

                if (_webSocket.State == WebSocketState.Open)
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == maxRetryAttempts)
                {
                    throw new TimeoutException($"Connection to {serverUri} timed out after {maxRetryAttempts} attempts");
                }
            }
            catch (WebSocketException) when (attempt < maxRetryAttempts)
            {
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        try
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnecting",
                linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }

    public ClientWebSocket GetUnderlyingSocket()
    {
        ThrowIfDisposed();
        return _webSocket;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await DisconnectAsync(CancellationToken.None);
            }
            catch
            {
            }
        }

        _webSocket.Dispose();
    }

    public Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IGameRequest
    {
        return SendMessageAsync(TRequest.MessageType, request, cancellationToken);
    }


    private async Task SendMessageAsync(string type, object payload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        var envelope = new ClientMessageEnvelope(type, payload);
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);

        using var timeoutCts = new CancellationTokenSource(_sendTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        await _webSocket.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            true,
            linkedCts.Token);
    }
}

