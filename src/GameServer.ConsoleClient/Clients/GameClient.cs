using GameServer.ConsoleClient.Model.Events;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace GameServer.ConsoleClient.Clients;

public sealed class GameClient(ILogger<GameClient> logger) : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket = new();
    private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(5);
    private readonly int _maxRetryAttempts = 3;
    private bool _isDisposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;

    public event Action<ServerMessageEnvelope>? OnMessageReceived;
    public event Action<GiftReceivedEvent>? OnGiftReceived;
    public event Action<FriendAddedEvent>? OnFriendAdded;
    public event Action<LoginResponse>? OnLoginResponse;
    public event Action<ErrorResponse>? OnError;
    public event Action<WebSocketCloseStatus?, string?>? OnDisconnected;

    public WebSocketState State => _webSocket.State;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    public async Task<bool> ConnectAsync(Uri serverUri, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        logger.LogInformation("Connecting to {ServerUri}...", serverUri);

        for (var attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                logger.LogDebug("Connection attempt {Attempt}/{MaxAttempts}", attempt, _maxRetryAttempts);

                using var timeoutCts = new CancellationTokenSource(_connectionTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCts.Token);

                await _webSocket.ConnectAsync(serverUri, linkedCts.Token);

                if (_webSocket.State == WebSocketState.Open)
                {
                    logger.LogInformation("Connected successfully to {ServerUri}", serverUri);
                    return true;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Connection attempt {Attempt} timed out", attempt);
                if (attempt == _maxRetryAttempts)
                {
                    throw new TimeoutException($"Connection to {serverUri} timed out after {_maxRetryAttempts} attempts");
                }
            }
            catch (WebSocketException ex) when (attempt < _maxRetryAttempts)
            {
                logger.LogWarning(ex, "Connection attempt {Attempt} failed, retrying in {Delay}ms", attempt, _retryDelay.TotalMilliseconds);
                await Task.Delay(_retryDelay, cancellationToken);
            }
        }

        logger.LogError("Failed to connect to {ServerUri} after {MaxAttempts} attempts", serverUri, _maxRetryAttempts);
        return false;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        logger.LogInformation("Disconnecting from server...");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        try
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnecting",
                linkedCts.Token);

            logger.LogInformation("Disconnected gracefully");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Disconnect timed out");
        }
        catch (WebSocketException ex)
        {
            logger.LogWarning(ex, "WebSocket error during disconnect");
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

        await StopListeningAsync();

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
        logger.LogDebug("GameClient disposed");
    }

    public Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IGameRequest
    {
        return SendMessageAsync(TRequest.MessageType, request, cancellationToken);
    }

    public void StartListening()
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        if (_listenerTask is not null)
        {
            return;
        }

        logger.LogDebug("Starting background listener");
        _listenerCts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(_listenerCts.Token));
    }

    public async Task StopListeningAsync()
    {
        if (_listenerCts is null || _listenerTask is null)
        {
            return;
        }

        logger.LogDebug("Stopping background listener");
        await _listenerCts.CancelAsync();

        try
        {
            await _listenerTask;
        }
        catch (OperationCanceledException)
        {
        }

        _listenerCts.Dispose();
        _listenerCts = null;
        _listenerTask = null;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);

        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                var message = await ReceiveFullMessageAsync(buffer, cancellationToken);

                if (message is null)
                {
                    break;
                }

                ProcessMessage(message.Value);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            logger.LogWarning(ex, "WebSocket error in listener");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            var closeStatus = _webSocket.CloseStatus;
            var closeDescription = _webSocket.CloseStatusDescription;
            logger.LogInformation("Connection closed: {Status} - {Description}", closeStatus, closeDescription);
            OnDisconnected?.Invoke(closeStatus, closeDescription);
        }
    }

    private async Task<ReadOnlyMemory<byte>?> ReceiveFullMessageAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        using var messageStream = new MemoryStream();

        ValueWebSocketReceiveResult result;

        do
        {
            result = await _webSocket.ReceiveAsync(
                buffer.AsMemory(),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            messageStream.Write(buffer, 0, result.Count);

            if (messageStream.Length > 1024 * 1024)
            {
                throw new InvalidOperationException("Message exceeds maximum size of 1MB");
            }

        } while (!result.EndOfMessage);

        return messageStream.ToArray();
    }

    private void ProcessMessage(ReadOnlyMemory<byte> messageBytes)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<ServerMessageEnvelope>(messageBytes.Span, JsonOptions);

            logger.LogDebug("Received message: Type={Type}, Size={Size}bytes", envelope.Type, messageBytes.Length);

            OnMessageReceived?.Invoke(envelope);

            DispatchEvent(envelope);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize message");
            OnError?.Invoke(new ErrorResponse("PARSE_ERROR", "Failed to parse server message"));
        }
    }

    private void DispatchEvent(ServerMessageEnvelope envelope)
    {
        try
        {
            switch (envelope.Type)
            {
                case "GIFT_RECEIVED":
                    var gift = envelope.Payload.Deserialize<GiftReceivedEvent>(JsonOptions);
                    OnGiftReceived?.Invoke(gift);
                    break;

                case "FRIEND_ADDED":
                    var friendAdded = envelope.Payload.Deserialize<FriendAddedEvent>(JsonOptions);
                    OnFriendAdded?.Invoke(friendAdded);
                    break;

                case "LOGIN_RESPONSE":
                    var login = envelope.Payload.Deserialize<LoginResponse>(JsonOptions);
                    OnLoginResponse?.Invoke(login);
                    break;

                case "ERROR":
                    var error = envelope.Payload.Deserialize<ErrorResponse>(JsonOptions);
                    OnError?.Invoke(error);
                    break;
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize event payload for type {Type}", envelope.Type);
            OnError?.Invoke(new ErrorResponse("PARSE_ERROR", "Failed to parse server message"));
        }
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

        logger.LogDebug("Sending message: Type={Type}, Size={Size}bytes", type, messageBytes.Length);

        using var timeoutCts = new CancellationTokenSource(_sendTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        await _webSocket.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            true,
            linkedCts.Token);

        logger.LogDebug("Message sent successfully: Type={Type}", type);
    }
}
