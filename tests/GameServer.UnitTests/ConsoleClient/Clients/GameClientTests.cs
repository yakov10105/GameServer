using System.Net.WebSockets;
using GameServer.ConsoleClient.Clients;

namespace GameServer.UnitTests.ConsoleClient.Clients;

public class GameClientTests : IAsyncDisposable
{
    private GameClient? _client;

    [Fact]
    public void Constructor_WithDefaultParameters_ShouldCreateClient()
    {
        _client = new GameClient();

        Assert.NotNull(_client);
        Assert.Equal(WebSocketState.None, _client.State);
        Assert.False(_client.IsConnected);
    }

    [Fact]
    public void Constructor_WithCustomTimeout_ShouldAcceptValue()
    {
        var customTimeout = TimeSpan.FromSeconds(30);

        _client = new GameClient(connectionTimeout: customTimeout);

        Assert.NotNull(_client);
    }

    [Fact]
    public void Constructor_WithCustomRetryAttempts_ShouldAcceptValue()
    {
        var customRetryAttempts = 5;

        _client = new GameClient(maxRetryAttempts: customRetryAttempts);

        Assert.NotNull(_client);
    }

    [Fact]
    public void Constructor_WithCustomRetryDelay_ShouldAcceptValue()
    {
        var customRetryDelay = TimeSpan.FromSeconds(5);

        _client = new GameClient(retryDelay: customRetryDelay);

        Assert.NotNull(_client);
    }

    [Fact]
    public void State_WhenNotConnected_ShouldReturnNone()
    {
        _client = new GameClient();

        var state = _client.State;

        Assert.Equal(WebSocketState.None, state);
    }

    [Fact]
    public void IsConnected_WhenNotConnected_ShouldReturnFalse()
    {
        _client = new GameClient();

        var isConnected = _client.IsConnected;

        Assert.False(isConnected);
    }

    [Fact]
    public void GetUnderlyingSocket_WhenNotDisposed_ShouldReturnSocket()
    {
        _client = new GameClient();

        var socket = _client.GetUnderlyingSocket();

        Assert.NotNull(socket);
        Assert.IsType<ClientWebSocket>(socket);
    }

    [Fact]
    public async Task GetUnderlyingSocket_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        _client = new GameClient();
        await _client.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _client.GetUnderlyingSocket());
    }

    [Fact]
    public async Task ConnectAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        _client = new GameClient();
        await _client.DisposeAsync();
        var serverUri = new Uri("ws://localhost:8080");

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _client.ConnectAsync(serverUri));
    }

    [Fact]
    public async Task DisconnectAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        _client = new GameClient();
        await _client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _client.DisconnectAsync());
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ShouldReturnImmediately()
    {
        _client = new GameClient();

        await _client.DisconnectAsync();

        Assert.Equal(WebSocketState.None, _client.State);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        _client = new GameClient();

        await _client.DisposeAsync();
        await _client.DisposeAsync();
        await _client.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _client.GetUnderlyingSocket());
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_ShouldDisposeSocket()
    {
        _client = new GameClient();
        var socket = _client.GetUnderlyingSocket();

        await _client.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _client.GetUnderlyingSocket());
    }

    [Fact]
    public async Task ConnectAsync_WithCancellationRequested_ShouldThrowOperationCanceledException()
    {
        _client = new GameClient(connectionTimeout: TimeSpan.FromMilliseconds(100));
        var serverUri = new Uri("ws://localhost:8080");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _client.ConnectAsync(serverUri, cts.Token));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Constructor_WithVariousRetryAttempts_ShouldAcceptAllValues(int retryAttempts)
    {
        _client = new GameClient(maxRetryAttempts: retryAttempts);

        Assert.NotNull(_client);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void Constructor_WithVariousTimeouts_ShouldAcceptAllValues(int timeoutMs)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);

        _client = new GameClient(connectionTimeout: timeout);

        Assert.NotNull(_client);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            try
            {
                await _client.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}

