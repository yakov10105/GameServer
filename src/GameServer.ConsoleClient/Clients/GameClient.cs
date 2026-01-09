namespace GameServer.ConsoleClient.Clients;

public sealed class GameClient : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket;
    private readonly TimeSpan _connectionTimeout;
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _retryDelay;
    private bool _isDisposed;

    public GameClient(
        TimeSpan? connectionTimeout = null,
        int maxRetryAttempts = 3,
        TimeSpan? retryDelay = null)
    {
        _webSocket = new ClientWebSocket();
        _connectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(10);
        _maxRetryAttempts = maxRetryAttempts;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
    }

    public WebSocketState State => _webSocket.State;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    public async Task<bool> ConnectAsync(Uri serverUri, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        for (var attempt = 1; attempt <= _maxRetryAttempts; attempt++)
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
                if (attempt == _maxRetryAttempts)
                {
                    throw new TimeoutException($"Connection to {serverUri} timed out after {_maxRetryAttempts} attempts");
                }
            }
            catch (WebSocketException) when (attempt < _maxRetryAttempts)
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
                await DisconnectAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        _webSocket.Dispose();
    }
}

